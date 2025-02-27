﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.IO;
using System.Collections.Generic;
using Mooege.Common;
using Mooege.Common.Helpers;
using Mooege.Core.GS.Actors;
using Mooege.Core.GS.Map;
using Mooege.Net.GS;
using Mooege.Net.GS.Message;
using Mooege.Net.GS.Message.Definitions.ACD;
using Mooege.Net.GS.Message.Definitions.Animation;
using Mooege.Net.GS.Message.Definitions.Attribute;
using Mooege.Net.GS.Message.Definitions.Combat;
using Mooege.Net.GS.Message.Definitions.Effect;
using Mooege.Net.GS.Message.Definitions.Misc;
using Mooege.Net.GS.Message.Definitions.Player;
using Mooege.Net.GS.Message.Fields;

namespace Mooege.Core.GS.Universe
{
    public class Universe : IMessageConsumer
    {
        static readonly Logger Logger = LogManager.CreateLogger();

        private readonly List<World> _worlds;

        public PlayerManager PlayerManager { get; private set; }
        int ObjectId = 0x78f50114 + 100;

        public Universe()
        {
            this._worlds = new List<World>();
            this.PlayerManager = new PlayerManager(this);

            InitializeUniverse();
        }

        public int NextObjectId { get { return ObjectId++; } }

        public void Route(GameClient client, GameMessage message)
        {
            switch (message.Consumer)
            {
                case Consumers.Universe:
                    this.Consume(client, message);
                    break;
                case Consumers.PlayerManager:
                    this.PlayerManager.Consume(client, message);
                    break;
                case Consumers.Hero:
                    client.Player.Hero.Consume(client, message);
                    break;
            }
        }

        public void Consume(GameClient client, GameMessage message)
        {
            if (message is TargetMessage) OnToonTargetChange(client, (TargetMessage)message);
        }

        void InitializeUniverse()
        {
            LoadUniverseData("Assets/Maps/universe.txt");
        }

        private void LoadUniverseData(string Filename)
        {
            if (File.Exists(Filename))
            {
                StreamReader file = null;
                try
                {
                    var rx = new System.Text.RegularExpressions.Regex(@"\s+");

                    string line;
                    file = new StreamReader(Filename);
                    while ((line = file.ReadLine()) != null)
                    {
                        line = rx.Replace(line, @" ");
                        string[] data = line.Split(' ');

                        if (data.Length == 0) continue; //check only lines with data in them

                        //packet data
                        if (data[0].Equals("p") && data.Length >= 2)
                        {
                            int packettype = int.Parse(data[1]);
                            switch (packettype)
                            {
                                case 0x34: //new scene
                                    {
                                        int WorldID = int.Parse(data[2]);
                                        World w = GetWorld(WorldID);
                                        w.AddScene(line);
                                    }
                                    break;

                                case 0x37: //reveal world
                                    {
                                        int WorldID = int.Parse(data[2]);
                                        World w = GetWorld(WorldID);
                                        w.WorldSNO = int.Parse(data[3]);
                                    }
                                    break;

                                case 0x3b: //new actor     
                                    {
                                        int WorldID = int.Parse(data[16]);
                                        World w = GetWorld(WorldID);
                                        w.AddActor(line);
                                    }
                                    break;

                                case 0x44: //new map scene 
                                    {
                                        int WorldID = int.Parse(data[11]);
                                        World w = GetWorld(WorldID);
                                        w.AddMapScene(line);
                                    }
                                    break;

                                case 0x4b: //new portal
                                    {
                                        Actor a = GetActor(int.Parse(data[2]));
                                        if (a != null)
                                        {
                                            World w = GetWorld(a.WorldId);
                                            if (w != null) w.AddPortal(line);
                                        }
                                    }
                                    break;

                                default:
                                    //Logger.Info("Unimplemented packet type encountered in universe file: " + packettype);
                                    break;
                            }
                        }

                        //manual portal description
                        if (data[0].Equals("o") && data.Length >= 6)
                        {
                            Portal p = GetPortal(int.Parse(data[1]));
                            if (p != null)
                            {
                                p.TargetPos = new Vector3D();
                                p.TargetPos.X = float.Parse(data[2], System.Globalization.CultureInfo.InvariantCulture);
                                p.TargetPos.Y = float.Parse(data[3], System.Globalization.CultureInfo.InvariantCulture);
                                p.TargetPos.Z = float.Parse(data[4], System.Globalization.CultureInfo.InvariantCulture);
                                p.TargetWorldID = int.Parse(data[5]);
                            }
                        }


                        ////spawn point
                        //if (data[0].Equals("s") && data.Length >= 4)
                        //{
                        //    posx = float.Parse(data[1], System.Globalization.CultureInfo.InvariantCulture);
                        //    posy = float.Parse(data[2], System.Globalization.CultureInfo.InvariantCulture);
                        //    posz = float.Parse(data[3], System.Globalization.CultureInfo.InvariantCulture);
                        //}

                    }
                }
                catch (Exception e)
                {
                    Logger.DebugException(e, "LoadUniverseData");
                }
                finally
                {
                    if (file != null)
                        file.Close();
                }
            }
            else
            {
                Logger.Error("Universe file {0} not found!", Filename);
            }


            foreach (World w in _worlds)
                w.SortScenes();
        }

        public World GetWorld(int WorldID)
        {
            for (int x = 0; x < _worlds.Count; x++)
                if (_worlds[x].WorldID == WorldID) return _worlds[x];

            var world = new World(WorldID);
            _worlds.Add(world);
            return world;
        }

        Actor GetActor(int ActorID)
        {
            for (int x = 0; x < _worlds.Count; x++)
            {
                Actor a = _worlds[x].GetActor(ActorID);
                if (a != null) return a;
            }
            return null;
        }

        Portal GetPortal(int ActorID)
        {
            for (int x = 0; x < _worlds.Count; x++)
            {
                Portal p = _worlds[x].GetPortal(ActorID);
                if (p != null) return p;
            }
            return null;
        }

        public void ChangeToonWorld(GameClient client, int WorldID, Vector3D Pos)
        {
            Hero hero = client.Player.Hero;

            World newworld = null;
            //don't use getworld() here as that'd create a new empty world anyway
            foreach (var x in _worlds)
                if (x.WorldID == WorldID)
                    newworld = x;

            World currentworld = null;
            //don't use getworld() here as that'd create a new empty world anyway
            foreach (var x in _worlds)
                if (x.WorldID == hero.WorldId)
                    currentworld = x;
            
            if (newworld == null || currentworld==null) return; //don't go to a world we don't have in the universe

            currentworld.DestroyWorld(hero);

            hero.WorldId = newworld.WorldID;
            hero.CurrentWorldSNO = newworld.WorldSNO;
            hero.Position.X = Pos.X;
            hero.Position.Y = Pos.Y;
            hero.Position.Z = Pos.Z;

            newworld.Reveal(hero);

            client.SendMessage(new ACDWorldPositionMessage
            {
                Id = 0x3f,
                Field0 = hero.Id,
                Field1 = new WorldLocationMessageData
                {
                    Field0 = 1.43f,
                    Field1 = new PRTransform
                    {
                        Field0 = new Quaternion
                        {
                            Amount = 0.05940768f,
                            Axis = new Vector3D
                            {
                                X = 0f,
                                Y = 0f,
                                Z = 0.9982339f,
                            }
                        },
                        ReferencePoint = hero.Position,
                    },
                    Field2 = newworld.WorldID,
                }
            });

            client.FlushOutgoingBuffer();

            client.SendMessage(new PlayerWarpedMessage()
            {
                Id = 0x0B1,
                Field0 = 9,
                Field1 = 0f,
            });

            client.PacketId += 40 * 2;
            client.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = client.PacketId,
            });

            client.FlushOutgoingBuffer();
        }

        private void OnToonTargetChange(GameClient client, TargetMessage message)
        {
            //Logger.Info("Player interaction with " + message.AsText());

            Portal p=GetPortal(message.Field1);

            if (p!=null)
            {
                //we have a transition between worlds here
                ChangeToonWorld(client, p.TargetWorldID, p.TargetPos); //targetpos will always be valid as otherwise the portal wouldn't be targetable
                return;
            }

            else if (client.ObjectIdsSpawned == null || !client.ObjectIdsSpawned.Contains(message.Field1)) return;

            client.ObjectIdsSpawned.Remove(message.Field1);

            var killAni = new int[]{
                    0x2cd7,
                    0x2cd4,
                    0x01b378,
                    0x2cdc,
                    0x02f2,
                    0x2ccf,
                    0x2cd0,
                    0x2cd1,
                    0x2cd2,
                    0x2cd3,
                    0x2cd5,
                    0x01b144,
                    0x2cd6,
                    0x2cd8,
                    0x2cda,
                    0x2cd9
            };
            client.SendMessage(new PlayEffectMessage()
            {
                Id = 0x7a,
                Field0 = message.Field1,
                Field1 = 0x0,
                Field2 = 0x2,
            });
            client.SendMessage(new PlayEffectMessage()
            {
                Id = 0x7a,
                Field0 = message.Field1,
                Field1 = 0xc,
            });
            client.SendMessage(new PlayHitEffectMessage()
            {
                Id = 0x7b,
                Field0 = message.Field1,
                Field1 = client.Player.Hero.Id,
                Field2 = 0x2,
                Field3 = false,
            });

            client.SendMessage(new FloatingNumberMessage()
            {
                Id = 0xd0,
                Field0 = message.Field1,
                Field1 = 9001.0f,
                Field2 = 0,
            });

            client.SendMessage(new ANNDataMessage()
            {
                Id = 0x6d,
                Field0 = message.Field1,
            });

            int ani = killAni[RandomHelper.Next(killAni.Length)];
            //Logger.Info("Ani used: " + ani);

            client.SendMessage(new PlayAnimationMessage()
            {
                Id = 0x6c,
                Field0 = message.Field1,
                Field1 = 0xb,
                Field2 = 0,
                tAnim = new PlayAnimationMessageSpec[1]
                {
                    new PlayAnimationMessageSpec()
                    {
                        Field0 = 0x2,
                        Field1 = ani,
                        Field2 = 0x0,
                        Field3 = 1f
                    }
                }
            });

            client.PacketId += 10 * 2;
            client.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = client.PacketId,
            });

            client.SendMessage(new ANNDataMessage()
            {
                Id = 0xc5,
                Field0 = message.Field1,
            });

            GameAttributeMap attribs = new GameAttributeMap();
            attribs[GameAttribute.Hitpoints_Cur] = 0f;
            attribs[GameAttribute.Could_Have_Ragdolled] = true;
            attribs[GameAttribute.Deleted_On_Server] = true;
            attribs.SendMessage(client, message.Field1);

            client.SendMessage(new PlayEffectMessage()
            {
                Id = 0x7a,
                Field0 = message.Field1,
                Field1 = 0xc,
            });
            client.SendMessage(new PlayEffectMessage()
            {
                Id = 0x7a,
                Field0 = message.Field1,
                Field1 = 0x37,
            });
            client.SendMessage(new PlayHitEffectMessage()
            {
                Id = 0x7b,
                Field0 = message.Field1,
                Field1 = client.Player.Hero.Id,
                Field2 = 0x2,
                Field3 = false,
            });
            client.PacketId += 10 * 2;
            client.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = client.PacketId,
            });
        }

        public void SpawnMob(GameClient client, int mobId) // this shoudn't even rely on client or it's position though i know this is just a hack atm ;) /raist.
        {
            int nId = mobId;
            if (client.Player.Hero.Position == null)
                return;

            if (client.ObjectIdsSpawned == null)
            {
                client.ObjectIdsSpawned = new List<int>();
                // why is this necessary?
                //client.ObjectIdsSpawned.Add(client.ObjectId - 100);
                client.ObjectIdsSpawned.Add(this.NextObjectId);
            }

            int objectId = this.NextObjectId;
            client.ObjectIdsSpawned.Add(objectId);

            #region ACDEnterKnown Hittable Zombie
            client.SendMessage(new ACDEnterKnownMessage()
            {
                Id = 0x003B,
                Field0 = objectId,
                Field1 = nId,
                Field2 = 0x8,
                Field3 = 0x0,
                Field4 = new WorldLocationMessageData()
                {
                    Field0 = 1.35f,
                    Field1 = new PRTransform()
                    {
                        Field0 = new Quaternion()
                        {
                            Amount = 0.768145f,
                            Axis = new Vector3D()
                            {
                                X = 0f,
                                Y = 0f,
                                Z = -0.640276f,
                            },
                        },
                        ReferencePoint = new Vector3D()
                        {
                            X = client.Player.Hero.Position.X + 5,
                            Y = client.Player.Hero.Position.Y + 5,
                            Z = client.Player.Hero.Position.Z,
                        },
                    },
                    Field2 = 0x772E0000,
                },
                Field5 = null,
                Field6 = new GBHandle()
                {
                    Field0 = 1,
                    Field1 = 1,
                },
                Field7 = 0x00000001,
                Field8 = nId,
                Field9 = 0x0,
                Field10 = 0x0,
                Field11 = 0x0,
                Field12 = 0x0,
                Field13 = 0x0
            });
            client.SendMessage(new AffixMessage()
            {
                Id = 0x48,
                Field0 = objectId,
                Field1 = 0x1,
                aAffixGBIDs = new int[0]
            });
            client.SendMessage(new AffixMessage()
            {
                Id = 0x48,
                Field0 = objectId,
                Field1 = 0x2,
                aAffixGBIDs = new int[0]
            });
            client.SendMessage(new ACDCollFlagsMessage
            {
                Id = 0xa6,
                Field0 = objectId,
                Field1 = 0x1
            });

            GameAttributeMap attribs = new GameAttributeMap();
            attribs[GameAttribute.Untargetable] = false;
            attribs[GameAttribute.Uninterruptible] = true;
            attribs[GameAttribute.Buff_Visual_Effect, 1048575] = true;            
            attribs[GameAttribute.Buff_Icon_Count0, 30582] = 1;
            attribs[GameAttribute.Buff_Icon_Count0, 30286] = 1;
            attribs[GameAttribute.Buff_Icon_Count0, 30285] = 1;
            attribs[GameAttribute.Buff_Icon_Count0, 30284] = 1;
            attribs[GameAttribute.Buff_Icon_Count0, 30283] = 1;
            attribs[GameAttribute.Buff_Icon_Count0, 30290] = 1;
            attribs[GameAttribute.Buff_Icon_Count0, 79486] = 1;
            attribs[GameAttribute.Buff_Active, 30286] = true;
            attribs[GameAttribute.Buff_Active, 30285] = true;
            attribs[GameAttribute.Buff_Active, 30284] = true;
            attribs[GameAttribute.Buff_Active, 30283] = true;
            attribs[GameAttribute.Buff_Active, 30290] = true;

            attribs[GameAttribute.Hitpoints_Max_Total] = 4.546875f;
            attribs[GameAttribute.Buff_Active, 79486] = true;
            attribs[GameAttribute.Hitpoints_Max] = 4.546875f;
            attribs[GameAttribute.Hitpoints_Total_From_Level] = 0f;
            attribs[GameAttribute.Hitpoints_Cur] = 4.546875f;
            attribs[GameAttribute.Invulnerable] = true;
            attribs[GameAttribute.Buff_Active, 30582] = true;
            attribs[GameAttribute.TeamID] = 10;
            attribs[GameAttribute.Level] = 1;

            attribs.SendMessage(client, objectId);


            client.SendMessage(new ACDGroupMessage
            {
                Id = 0xb8,
                Field0 = objectId,
                Field1 = unchecked((int)0xb59b8de4),
                Field2 = unchecked((int)0xffffffff)
            });

            client.SendMessage(new ANNDataMessage
            {
                Id = 0x3e,
                Field0 = objectId
            });

            client.SendMessage(new ACDTranslateFacingMessage
            {
                Id = 0x70,
                Field0 = objectId,
                Field1 = (float)(RandomHelper.NextDouble() * 2.0 * Math.PI),
                Field2 = false
            });

            client.SendMessage(new SetIdleAnimationMessage
            {
                Id = 0xa5,
                Field0 = objectId,
                Field1 = 0x11150
            });

            client.SendMessage(new SNONameDataMessage
            {
                Id = 0xd3,
                Field0 = new SNOName
                {
                    Field0 = 0x1,
                    Field1 = nId
                }
            });
            #endregion

            //client.PacketId += 30 * 2;
            client.SendMessage(new DWordDataMessage()
            {
                Id = 0x89,
                Field0 = client.PacketId,
            });
            client.Tick += 20;
            client.SendMessage(new EndOfTickMessage()
            {
                Id = 0x008D,
                Field0 = client.Tick - 20,
                Field1 = client.Tick
            });
        }
    }
}
