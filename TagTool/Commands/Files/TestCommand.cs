using TagTool.Cache;
using TagTool.Common;
using TagTool.Geometry;
using TagTool.IO;
using TagTool.Scripting;
using TagTool.Serialization;
using TagTool.Tags.Definitions;
using TagTool.Tags.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using TagTool.Tags;
using System.Runtime.CompilerServices;
using System.Diagnostics;

namespace TagTool.Commands.Files
{
    class TestCommand : Command
    {
        private HaloOnlineCacheContext CacheContext { get; }
        private static bool debugConsoleWrite = true;
        private static List<string> csvQueue1 = new List<string>();
        private static List<string> csvQueue2 = new List<string>();

        public TestCommand(HaloOnlineCacheContext cacheContext) :
            base(true,

                "Test",
                "A test command.",

                "Test",

                "A test command. Used for various testing and temporary functionality.\n" +
                "Example setinvalidmaterials: 'Test setinvalidmaterials <ED mode or sbsp tag>'. Set all materials to 0x101F shaders\\invalid. \n\n")
        {
            CacheContext = cacheContext;
        }

        public override object Execute(List<string> args)
        {
            if (args.Count == 0)
                return false;

            var name = args[0].ToLower();
            args.RemoveAt(0);

            var commandsList = new Dictionary<string, string>
            {
                { "scriptingxml", "scriptingxml" },
                { "lensunknown", "lensunknown" },
                { "setinvalidmaterials", "Set all materials to shaders\\invalid or 0x101F to a provided mode or sbsp tag." },
                { "namemodetags", "Name all mode tags based on" },
                { "dumpforgepalettecommands", "Read a scnr tag's forge palettes and dump as a tagtool commands script." },
                { "dumpcommandsscript", "Extract all the tags of a mode or sbsp tag (rmt2, rm--) and generate a commands script. WIP" },
                { "shadowfix", "Hack/fix a weapon or forge object's shadow mesh." },
                { "namermt2", "Name all rmt2 tags based on their parent render method." },
                { "comparetags", "Compare and dump differences between two tags. Works between this and a different ms23 cache." },
                { "findconicaleffects", "" },
                { "mergeglobaltags", "Merges matg/mulg tags ported from legacy cache files into single Halo Online format matg/mulg tags." },
                { "cisc", "" },
                { "dumpscripts", "Dump scripts, usable with hardcoded scripts setup (text dump)" },
                { "defaultbitmaptypes", "" },
                { "mergetagnames", "" }
            };

            switch (name)
            {
                case "scriptingxml": return ScriptingXml(args);
                case "lensunknown": return LensUnknown(args);
                case "setinvalidmaterials": return SetInvalidMaterials(args);
                case "dumpforgepalettecommands": return DumpForgePaletteCommands(args);
                case "dumpcommandsscript": return DumpCommandsScript(args);
                case "temp": return Temp(args);
                case "shadowfix": return ShadowFix(args);
                case "namermt2": return NameRmt2();
                case "findconicaleffects": return FindConicalEffects();
                case "mergeglobaltags": return MergeGlobalTags(args);
                case "cisc": return Cisc(args);
                case "defaultbitmaptypes": return DefaultBitmapTypes(args);
                case "mergetagnames": return MergeTagNames(args);
                case "adjustscriptsfromfile": return AdjustScriptsFromFile(args);
                case "batchtagdepadd": return BatchTagDepAdd(args);
                case "namemodetags": return NameModeTags();
                case "nameblocsubtags": return NameBlocSubtags();
                case "nameeffesubtags": return NameEffeSubtags();
                case "namegameobjectssubtags": return NameGameObjectsSubtags();
                case "namemodelsubtags": return NameModelSubtags();
                case "namemodeshaders": return NameModeShaders();
                case "namefootsnd": return NameFootSnd();
                case "nameglobalmaterials": return NameGlobalMaterials();
                case "namelsndsubtags": return NameLsndSubtags();
                case "setupmulg": return SetupMulg();
                case "listprematchcameras": return ListPrematchCameras();
                case "findnullshaders": return FindNullShaders();
                default:
                    Console.WriteLine($"Invalid command: {name}");
                    Console.WriteLine($"Available commands: {commandsList.Count}");
                    foreach (var a in commandsList)
                        Console.WriteLine($"{a.Key}: {a.Value}");
                    return false;
            }
        }

        private bool FindNullShaders()
        {
            using (var cacheStream = CacheContext.OpenTagCacheRead())
            {
                foreach (var tag in CacheContext.TagCache.Index.FindAllInGroups("beam", "cntl", "decs", "ltvl", "prt3", "rm  "))
                {
                    if (tag == null)
                        continue;

                    var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag);
                    var tagDefinition = CacheContext.Deserialize(tagContext, TagDefinition.Find(tag.Group));

                    RenderMethod rmDefinition = null;

                    switch (tagDefinition)
                    {
                        case BeamSystem beam:
                            rmDefinition = beam.Beam[0].RenderMethod;
                            break;

                        case ContrailSystem cntl:
                            rmDefinition = cntl.Contrail[0].RenderMethod;
                            break;

                        case DecalSystem decs:
                            rmDefinition = decs.Decal[0].RenderMethod;
                            break;

                        case LightVolumeSystem ltvl:
                            rmDefinition = ltvl.LightVolume[0].RenderMethod;
                            break;

                        case Particle prt3:
                            rmDefinition = prt3.RenderMethod;
                            break;

                        case RenderMethod rm:
                            rmDefinition = rm;
                            break;
                    }

                    if (rmDefinition.ShaderProperties[0].Template.Index == -1)
                    {
                        var tagName = CacheContext.TagNames.ContainsKey(tag.Index) ?
                            CacheContext.TagNames[tag.Index] :
                            $"0x{tag.Index:X4}";

                        Console.WriteLine($"[{tag.Group.Tag}, 0x{tag.Index:X4}] {tagName}.{CacheContext.GetString(tag.Group.Name)}");
                    }
                }
            }

            return true;
        }

        private bool ListPrematchCameras()
        {
            using (var cacheStream = CacheContext.OpenTagCacheRead())
            {
                foreach (var scnrTag in CacheContext.TagCache.Index.FindAllInGroup("scnr"))
                {
                    if (scnrTag == null)
                        continue;

                    var scnrContext = new TagSerializationContext(cacheStream, CacheContext, scnrTag);
                    var scnrDefinition = CacheContext.Deserialize<Scenario>(scnrContext);

                    foreach (var cameraPoint in scnrDefinition.CutsceneCameraPoints)
                    {
                        if (cameraPoint.Name == "prematch_camera")
                        {
                            Console.WriteLine($"case @\"{CacheContext.TagNames[scnrTag.Index]}\":");
                            Console.WriteLine($"    createPrematchCamera = true;");
                            Console.WriteLine($"    position = new RealPoint3d({cameraPoint.Position.X}f, {cameraPoint.Position.Y}f, {cameraPoint.Position.Z}f);");
                            Console.WriteLine($"    orientation = new RealEulerAngles3d(Angle.FromDegrees({cameraPoint.Orientation.Yaw.Degrees}f), Angle.FromDegrees({cameraPoint.Orientation.Pitch.Degrees}f), Angle.FromDegrees({cameraPoint.Orientation.Roll.Degrees}f));");
                            Console.WriteLine($"    break;");
                            break;
                        }
                    }
                }
            }

            return true;
        }

        private bool SetupMulg()
        {
            using (var stream = CacheContext.OpenTagCacheReadWrite())
            {
                var mulgContext = new TagSerializationContext(stream, CacheContext, CacheContext.TagCache.Index.FindFirstInGroup("mulg"));
                var mulgDefinition = CacheContext.Deserialize<MultiplayerGlobals>(mulgContext);

                mulgDefinition.Universal[0].GameVariantWeapons = new List<MultiplayerGlobals.UniversalBlock.GameVariantWeapon>
            {
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("battle_rifle"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\battle_rifle\battle_rifle")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("assault_rifle"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\assault_rifle\assault_rifle")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("plasma_pistol"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\pistol\plasma_pistol\plasma_pistol")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("spike_rifle"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\spike_rifle\spike_rifle")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("smg"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\smg\smg")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("carbine"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\covenant_carbine\covenant_carbine")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("energy_sword"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\melee\energy_blade\energy_blade")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("magnum"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\pistol\magnum\magnum")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("needler"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\pistol\needler\needler")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("plasma_rifle"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\plasma_rifle\plasma_rifle")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("rocket_launcher"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\support_high\rocket_launcher\rocket_launcher")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("shotgun"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\shotgun\shotgun")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("sniper_rifle"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\sniper_rifle\sniper_rifle")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("brute_shot"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\support_low\brute_shot\brute_shot")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("unarmed"),
                    RandomChance = 0,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\melee\energy_blade\energy_blade_useless")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("beam_rifle"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\beam_rifle\beam_rifle")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("spartan_laser"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\support_high\spartan_laser\spartan_laser")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("none"),
                    RandomChance = 0,
                    Weapon = null
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("gravity_hammer"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\melee\gravity_hammer\gravity_hammer")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("excavator"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\pistol\excavator\excavator")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("flamethrower"),
                    RandomChance = 0,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\turret\flamethrower\flamethrower")
                },
                new MultiplayerGlobals.UniversalBlock.GameVariantWeapon
                {
                    Name = CacheContext.GetStringId("missile_pod"),
                    RandomChance = 0.1f,
                    Weapon = CacheContext.GetTag<Weapon>(@"objects\weapons\turret\missile_pod\missile_pod")
                }
            };

                mulgDefinition.Runtime[0].MultiplayerConstants[0].Weapons = new List<MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon>
                {
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // battle_rifle
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\battle_rifle\battle_rifle"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // carbine
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\covenant_carbine\covenant_carbine"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // sniper_rifle
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\sniper_rifle\sniper_rifle"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // beam_rifle
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\rifle\beam_rifle\beam_rifle"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // spartan_laster
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\support_high\spartan_laser\spartan_laser"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // rocket_launcher
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\support_high\rocket_launcher\rocket_launcher"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // chaingun_turret
                        Type = CacheContext.GetTag<Weapon>(@"objects\vehicles\warthog\turrets\chaingun\weapon\chaingun_turret"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // machinegun_turret
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\turret\machinegun_turret\machinegun_turret"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // machinegun_turret_integrated
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\turret\machinegun_turret\machinegun_turret_integrated"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // plasma_cannon
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\turret\plasma_cannon\plasma_cannon"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // plasma_cannon_integrated
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\turret\plasma_cannon\plasma_cannon_integrated"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // needler
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\pistol\needler\needler"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // flak_cannon
                        Type = CacheContext.GetTag<Weapon>(@"objects\weapons\support_high\flak_cannon\flak_cannon"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // gauss_turret
                        Type = CacheContext.GetTag<Weapon>(@"objects\vehicles\warthog\turrets\gauss\weapon\gauss_turret"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // anti_infantry
                        Type = CacheContext.GetTag<Weapon>(@"objects\vehicles\mauler\anti_infantry\weapon\anti_infantry"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    },
                    new MultiplayerGlobals.RuntimeBlock.MultiplayerConstant.Weapon
                    {
                        // behemoth_chaingun_turret
                        Type = null,// CacheContext.GetTag<Weapon>(@"objects\levels\multi\shrine\behemoth\weapon\behemoth_chaingun_turret"),
                        Unknown1 = 5.0f,
                        Unknown2 = 15.0f,
                        Unknown3 = 5.0f,
                        Unknown4 = -10.0f
                    }
                };
                
                #region Universal GameVariantVehicles
                mulgDefinition.Universal[0].GameVariantVehicles = new List<MultiplayerGlobals.UniversalBlock.GameVariantVehicle>
                {
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("warthog"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\warthog\warthog")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("ghost"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\ghost\ghost")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("scorpion"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\scorpion\scorpion")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("wraith"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\wraith\wraith")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("banshee"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\banshee\banshee")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("mongoose"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\mongoose\mongoose")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("chopper"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\brute_chopper\brute_chopper")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("mauler"),
                        Vehicle = null, //CacheContext.GetTagInstance<Vehicle>(@"objects\vehicles\mauler\mauler")
                    },
                    new MultiplayerGlobals.UniversalBlock.GameVariantVehicle
                    {
                        Name = CacheContext.GetStringId("hornet"),
                        Vehicle = CacheContext.GetTag<Vehicle>(@"objects\vehicles\hornet\hornet")
                    }
                };
                #endregion

                #region Universal VehicleSets
                mulgDefinition.Universal[0].VehicleSets = new List<MultiplayerGlobals.UniversalBlock.VehicleSet>
                {
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("default"),
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>()
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("no_vehicles"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("warthog"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("ghost"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("scorpion"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("wraith"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mongoose"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("banshee"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("chopper"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mauler"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("hornet"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("mongooses_only"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("warthog"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("ghost"),
                                SubstitutedVehicle = CacheContext.GetStringId("mongoose")
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("scorpion"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("wraith"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("banshee"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("chopper"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mauler"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("hornet"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("light_ground_only"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("scorpion"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("wraith"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("banshee"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("hornet"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("tanks_only"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("warthog"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("ghost"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mongoose"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("banshee"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("chopper"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mauler"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("hornet"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("aircraft_only"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("warthog"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("ghost"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("scorpion"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("wraith"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mongoose"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("chopper"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mauler"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("no_light_ground"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("warthog"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("ghost"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mongoose"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("chopper"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("mauler"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("no_tanks"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("scorpion"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("wraith"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("no_aircraft"),
                        #region Substitutions
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>
                        {
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("banshee"),
                                SubstitutedVehicle = StringId.Invalid
                            },
                            new MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution
                            {
                                OriginalVehicle = CacheContext.GetStringId("hornet"),
                                SubstitutedVehicle = StringId.Invalid
                            }
                        }
                        #endregion
                    },
                    new MultiplayerGlobals.UniversalBlock.VehicleSet
                    {
                        Name = CacheContext.GetStringId("all_vehicles"),
                        Substitutions = new List<MultiplayerGlobals.UniversalBlock.VehicleSet.Substitution>()
                    }
                };
                #endregion

                CacheContext.Serialize(mulgContext, mulgDefinition);
            }

            return true;
        }

        private bool MergeTagNames(List<string> args)
        {
            if (args.Count != 1)
                return false;

            var context = new HaloOnlineCacheContext(new DirectoryInfo(args[0]));
            context.LoadTagNames();

            foreach (var entry in context.TagNames)
            {
                if (entry.Key >= CacheContext.TagCache.Index.Count || CacheContext.TagCache.Index[entry.Key] == null)
                    continue;

                var srcTag = CacheContext.GetTag(entry.Key);
                var dstTag = context.GetTag(entry.Key);

                if (!srcTag.IsInGroup(dstTag.Group) || CacheContext.TagNames.ContainsKey(srcTag.Index))
                    continue;

                CacheContext.TagNames[srcTag.Index] = context.TagNames[dstTag.Index];
            }

            return true;
        }

        private bool DefaultBitmapTypes(List<string> args)
        {
            if (args.Count != 0)
                return false;

            using (var cacheStream = CacheContext.OpenTagCacheRead())
            {
                var defaultBitmapNames = new List<string>
                {
                    @"shaders\default_bitmaps\bitmaps\gray_50_percent",
                    @"shaders\default_bitmaps\bitmaps\alpha_grey50",
                    @"shaders\default_bitmaps\bitmaps\color_white",
                    @"shaders\default_bitmaps\bitmaps\default_detail",
                    @"shaders\default_bitmaps\bitmaps\reference_grids",
                    @"shaders\default_bitmaps\bitmaps\default_vector",
                    @"shaders\default_bitmaps\bitmaps\default_alpha_test",
                    @"shaders\default_bitmaps\bitmaps\default_dynamic_cube_map",
                    @"shaders\default_bitmaps\bitmaps\color_red",
                    @"shaders\default_bitmaps\bitmaps\alpha_white",
                    @"shaders\default_bitmaps\bitmaps\monochrome_alpha_grid",
                    @"shaders\default_bitmaps\bitmaps\gray_50_percent_linear",
                    @"shaders\default_bitmaps\bitmaps\color_black_alpha_black",
                    @"shaders\default_bitmaps\bitmaps\dither_pattern",
                    @"shaders\default_bitmaps\bitmaps\bump_detail",
                    @"shaders\default_bitmaps\bitmaps\color_black",
                    @"shaders\default_bitmaps\bitmaps\auto_exposure_weight",
                    @"shaders\default_bitmaps\bitmaps\dither_pattern2",
                    @"shaders\default_bitmaps\bitmaps\random4_warp",
                    @"levels\shared\bitmaps\nature\water\water_ripples",
                    @"shaders\default_bitmaps\bitmaps\vision_mode_mask"
                };

                var defaultBitmapTypes = new Dictionary<string, List<string>>();

                foreach (var tag in CacheContext.TagCache.Index)
                {
                    if (tag == null || !(tag.IsInGroup("rm  ") || tag.IsInGroup("prt3")))
                        continue;

                    var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag);
                    var tagDefinition = CacheContext.Deserialize(tagContext, TagDefinition.Find(tag.Group));

                    RenderMethod renderMethod = null;

                    switch (tagDefinition)
                    {
                        case RenderMethod rm:
                            renderMethod = rm;
                            break;

                        case Particle prt3:
                            renderMethod = prt3.RenderMethod;
                            break;
                    }

                    tagContext = new TagSerializationContext(cacheStream, CacheContext, renderMethod.ShaderProperties[0].Template);
                    var template = CacheContext.Deserializer.Deserialize<RenderMethodTemplate>(tagContext);

                    for (var i = 0; i < template.ShaderMaps.Count; i++)
                    {
                        var mapTemplate = template.ShaderMaps[i];
                        var mapName = CacheContext.GetString(mapTemplate.Name);

                        var mapShader = renderMethod.ShaderProperties[0].ShaderMaps[i];
                        var mapTagName = CacheContext.TagNames.ContainsKey(mapShader.Bitmap.Index) ?
                            CacheContext.TagNames[mapShader.Bitmap.Index] :
                            $"0x{mapShader.Bitmap.Index:X4}";

                        if (!mapTagName.StartsWith(@"shaders\default_bitmaps\"))
                            continue;

                        if (!defaultBitmapTypes.ContainsKey(mapTagName))
                            defaultBitmapTypes[mapTagName] = new List<string>();

                        if (!defaultBitmapTypes[mapTagName].Contains(mapName))
                            defaultBitmapTypes[mapTagName].Add(mapName);
                    }
                }

                foreach (var entry in defaultBitmapTypes)
                {
                    foreach (var type in entry.Value)
                        Console.WriteLine($"case \"{type}\":");
                    Console.WriteLine($"return @\"{entry.Key}\";");
                    Console.WriteLine("\tbreak;");
                    Console.WriteLine();
                }
            }

            return true;
        }

        private bool Cisc(List<string> args)
        {
            if (args.Count != 0)
                return false;

            using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
            {
                foreach (var tagInstance in CacheContext.TagCache.Index)
                {
                    if (tagInstance == null || !tagInstance.IsInGroup("cisc"))
                        continue;

                    var tagContext = new TagSerializationContext(cacheStream, CacheContext, tagInstance);
                    var tagDefinition = CacheContext.Deserialize<CinematicScene>(tagContext);

                    foreach (var shot in tagDefinition.Shots)
                    {
                        shot.LoadedFrameCount -= 1;

                        foreach (var sound in shot.Sounds)
                            sound.Frame = Math.Min(sound.Frame == 1 ? 1 : sound.Frame * 2, shot.LoadedFrameCount - 1);

                        foreach (var sound in shot.BackgroundSounds)
                            sound.Frame = Math.Min(sound.Frame == 1 ? 1 : sound.Frame * 2, shot.LoadedFrameCount - 1);

                        foreach (var effect in shot.Effects)
                            effect.Frame = Math.Min(effect.Frame == 1 ? 1 : effect.Frame * 2, shot.LoadedFrameCount - 1);

                        foreach (var effect in shot.ScreenEffects)
                        {
                            effect.StartFrame = Math.Min(effect.StartFrame == 1 ? 1 : effect.StartFrame * 2, shot.LoadedFrameCount - 1);
                            effect.EndFrame = Math.Min(effect.EndFrame == 1 ? 1 : effect.EndFrame * 2, shot.LoadedFrameCount - 1);
                        }

                        foreach (var script in shot.ImportScripts)
                            script.Frame = Math.Min(script.Frame == 1 ? 1 : script.Frame * 2, shot.LoadedFrameCount - 1);

                        for (var i = 0; i < shot.LoadedFrameCount; i++)
                        {
                            if (i + 2 >= shot.LoadedFrameCount)
                                break;

                            shot.Frames[i + 1].Flags = shot.Frames[i].Flags;
                        }
                    }

                    CacheContext.Serialize(tagContext, tagDefinition);
                }
            }

            return true;
        }

        private Globals MergeGlobals(List<Globals> matgs)
        {
            var matg = matgs[0];

            return matg;
        }

        private MultiplayerGlobals MergeMultiplayerGlobals(List<MultiplayerGlobals> mulgs)
        {
            var mulg = mulgs[0];

            return mulg;
        }

        private object MergeGlobalTags(List<string> args)
        {
            // initialize serialization contexts.
            var tagsContext = new TagSerializationContext(null, null, null);

            // find global tags
            var matg_tags = CacheContext.TagCache.Index.FindAllInGroup(new Tag("matg")).ToList();
            var mulg_tags = CacheContext.TagCache.Index.FindAllInGroup(new Tag("mulg")).ToList();

            using (var tagsStream = CacheContext.OpenTagCacheReadWrite())
            {
                var matgs = new List<Globals> { };
                var mulgs = new List<MultiplayerGlobals> { };

                // deserialize halo-online globals.
                foreach (var matg_tag in matg_tags)
                {
                    tagsContext = new TagSerializationContext(tagsStream, CacheContext, matg_tag);
                    matgs.Add(CacheContext.Deserializer.Deserialize<Globals>(tagsContext));
                }
                foreach (var mulg_tag in mulg_tags)
                {
                    tagsContext = new TagSerializationContext(tagsStream, CacheContext, mulg_tag);
                    mulgs.Add(CacheContext.Deserializer.Deserialize<MultiplayerGlobals>(tagsContext));
                }

                // merge global tags into the first global tag
                var matg = MergeGlobals(matgs);
                var mulg = MergeMultiplayerGlobals(mulgs);

                // serialize global tags
                tagsContext = new TagSerializationContext(tagsStream, CacheContext, matg_tags[0]);
                CacheContext.Serialize(tagsContext, matg);
                tagsContext = new TagSerializationContext(tagsStream, CacheContext, mulg_tags[0]);
                CacheContext.Serialize(tagsContext, mulg);
            }

            return true;
        }

        private bool FindConicalEffects()
        {
            using (var stream = CacheContext.TagCacheFile.OpenRead())
            using (var reader = new BinaryReader(stream))
            {
                for (var i = 0; i < CacheContext.TagCache.Index.Count; i++)
                {
                    var tag = CacheContext.GetTag(i);

                    if (tag == null || !tag.IsInGroup("effe"))
                        continue;

                    stream.Position = tag.HeaderOffset + tag.DefinitionOffset + 0x5C;
                    var conicalDistributionCount = reader.ReadInt32();

                    if (conicalDistributionCount <= 0)
                        continue;

                    var tagName = CacheContext.TagNames.ContainsKey(tag.Index) ?
                        $"0x{tag.Index:X4} - {CacheContext.TagNames[tag.Index]}" :
                        $"0x{tag.Index:X4}";

                    Console.WriteLine($"{tagName}.effect - {conicalDistributionCount} {(conicalDistributionCount == 1 ? "distribution" : "distributions")}");
                }
            }

            return true;
        }

        public void CsvDumpQueueToFile(List<string> in_, string file)
        {
            var fileOut = new FileInfo(file);
            if (File.Exists(file))
                File.Delete(file);

            int i = -1;
            using (var csvStream = fileOut.OpenWrite())
            using (var csvWriter = new StreamWriter(csvStream))
            {
                foreach (var a in in_)
                {
                    csvStream.Position = csvStream.Length;
                    csvWriter.WriteLine(a);
                    i++;
                }
            }
        }

        private CacheFile OpenCacheFile(string cacheArg)
        {
            FileInfo blamCacheFile = new FileInfo(cacheArg);

            // Console.WriteLine("Reading H3 cache file...");

            if (!blamCacheFile.Exists)
                throw new FileNotFoundException(blamCacheFile.FullName);

            CacheFile BlamCache = null;

            using (var fs = new FileStream(blamCacheFile.FullName, FileMode.Open, FileAccess.Read))
            {
                var reader = new EndianReader(fs, EndianFormat.BigEndian);

                var head = reader.ReadInt32();

                if (head == 1684104552)
                    reader.Format = EndianFormat.LittleEndian;

                var v = reader.ReadInt32();

                reader.SeekTo(284);
                var version = CacheVersionDetection.GetFromBuildName(reader.ReadString(32));

                switch (version)
                {
                    case CacheVersion.Halo2Xbox:
                    case CacheVersion.Halo2Vista:
                        BlamCache = new CacheFileGen2(CacheContext, blamCacheFile, version, false);
                        break;

                    case CacheVersion.Halo3Retail:
                    case CacheVersion.Halo3ODST:
                    case CacheVersion.HaloReach:
                        BlamCache = new CacheFileGen3(CacheContext, blamCacheFile, version, false);
                        break;

                    default:
                        throw new NotSupportedException(CacheVersionDetection.GetBuildName(version));
                }
            }

            // BlamCache.LoadResourceTags();

            return BlamCache;
        }

        private ScriptValueType.Halo3ODSTValue ParseScriptValueType(string value)
        {
            foreach (var option in Enum.GetNames(typeof(ScriptValueType.Halo3ODSTValue)))
                if (value.ToLower().Replace("_", "").Replace(" ", "") == option.ToLower().Replace("_", "").Replace(" ", ""))
                    return (ScriptValueType.Halo3ODSTValue)Enum.Parse(typeof(ScriptValueType.Halo3ODSTValue), option);

            throw new KeyNotFoundException(value);
        }

        private bool ScriptingXml(List<string> args)
        {
            if (args.Count != 0)
                return false;

            //
            // Load the lower-version scription xml file
            //


            Console.WriteLine();
            Console.WriteLine("Enter the path to the scripting xml:");
            Console.Write("> ");

            var xmlPath = Console.ReadLine();

            var xml = new XmlDocument();
            xml.Load(xmlPath);

            var scripts = new Dictionary<int, ScriptInfo>();

            foreach (XmlNode node in xml["BlamScript"]["functions"])
            {
                if (node.NodeType != XmlNodeType.Element)
                    continue;

                var script = new ScriptInfo(
                    ParseScriptValueType(node.Attributes["returnType"].InnerText),
                    node.Attributes["name"].InnerText);

                if (script.Name == "")
                    continue;

                if (node.HasChildNodes)
                {
                    foreach (XmlNode argumentNode in node.ChildNodes)
                    {
                        if (argumentNode.NodeType != XmlNodeType.Element)
                            continue;

                        script.Arguments.Add(new ScriptInfo.ArgumentInfo(ParseScriptValueType(argumentNode.Attributes["type"].InnerText)));
                    }
                }

                scripts[int.Parse(node.Attributes["opcode"].InnerText.Replace("0x", ""), NumberStyles.HexNumber)] = script;
            }

            Console.WriteLine();

            for (var opcode = 0; opcode < scripts.Keys.Max(); opcode++)
            {
                if (!scripts.ContainsKey(opcode))
                    continue;

                var script = scripts[opcode];

                if (script.Arguments.Count == 0)
                {
                    Console.WriteLine($"                [0x{opcode:X3}] = new ScriptInfo(ScriptValueType.{script.Type}, \"{script.Name}\"),");
                }
                else
                {
                    Console.WriteLine($"                [0x{opcode:X3}] = new ScriptInfo(ScriptValueType.{script.Type}, \"{script.Name}\")");
                    Console.WriteLine("                {");

                    foreach (var argument in script.Arguments)
                        Console.WriteLine($"                    new ScriptInfo.ArgumentInfo(ScriptValueType.{argument.Type}),");

                    Console.WriteLine("                },");
                }
            }

            Console.WriteLine();

            return true;
        }

        private bool LensUnknown(List<string> args)
        {
            if (args.Count != 0)
                return false;

            using (var cacheStream = CacheContext.OpenTagCacheRead())
            {
                foreach (var instance in CacheContext.TagCache.Index.FindAllInGroup("lens"))
                {
                    var context = new TagSerializationContext(cacheStream, CacheContext, instance);
                    var definition = CacheContext.Deserializer.Deserialize<LensFlare>(context);
                }
            }

            return true;
        }

        private static CachedTagInstance PortTagReference(HaloOnlineCacheContext cacheContext, CacheFile blamCache, int index)
        {
            if (index == -1)
                return null;

            var instance = blamCache.IndexItems.Find(i => i.ID == index);

            if (instance != null)
            {
                var tags = cacheContext.TagCache.Index.FindAllInGroup(instance.GroupTag);

                foreach (var tag in tags)
                {
                    if (!cacheContext.TagNames.ContainsKey(tag.Index))
                        continue;

                    if (instance.Name == cacheContext.TagNames[tag.Index])
                        return tag;
                }
            }

            return null;
        }

        public bool SetInvalidMaterials(List<string> args) // Set all mode or sbsp shaders to shaders\invalid 0x101F
        {
            Console.WriteLine("Required args: [0]ED tag; ");

            if (args.Count != 1)
                return false;

            string edTagArg = args[0];

            if (!CacheContext.TryGetTag(edTagArg, out var edTag))
                return false;

            if (edTag.IsInGroup("mode"))
            {
                RenderModel edMode;
                using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, edTag);
                    edMode = CacheContext.Deserializer.Deserialize<RenderModel>(edContext);
                }

                foreach (var a in edMode.Materials)
                    a.RenderMethod = CacheContext.GetTag(0x101F);

                using (var stream = CacheContext.TagCacheFile.Open(FileMode.Open, FileAccess.ReadWrite))
                {
                    var context = new TagSerializationContext(stream, CacheContext, edTag);
                    CacheContext.Serializer.Serialize(context, edMode);
                }
            }

            else if (edTag.IsInGroup("sbsp"))
            {
                ScenarioStructureBsp instance;
                using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, edTag);
                    instance = CacheContext.Deserializer.Deserialize<ScenarioStructureBsp>(edContext);
                }

                foreach (var a in instance.Materials)
                    a.RenderMethod = CacheContext.GetTag(0x101F);

                Console.WriteLine("Nuked shaders.");

                using (var stream = CacheContext.TagCacheFile.Open(FileMode.Open, FileAccess.ReadWrite))
                {
                    var context = new TagSerializationContext(stream, CacheContext, edTag);
                    CacheContext.Serializer.Serialize(context, instance);
                }
            }

            return true;
        }

        public bool DumpForgePaletteCommands(List<string> args) // Dump all the forge lists of a scnr to use as tagtool commands. Mainly to reorder the items easily
        {
            Console.WriteLine("Required args: [0]ED scnr tag; ");

            if (args.Count != 1 || !CacheContext.TryGetTag(args[0], out var edTag))
                return false;

            Scenario instance;
            using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
            {
                var edContext = new TagSerializationContext(cacheStream, CacheContext, edTag);
                instance = CacheContext.Deserializer.Deserialize<Scenario>(edContext);
            }

            Console.WriteLine($"RemoveBlockElements SandboxEquipment 0 *");
            foreach (var a in instance.SandboxEquipment)
            {
                Console.WriteLine($"AddBlockElements SandboxEquipment 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField SandboxEquipment[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField SandboxEquipment[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField SandboxEquipment[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            string type = "SandboxWeapons";
            Console.WriteLine($"RemoveBlockElements {type} 0 *");
            foreach (var a in instance.SandboxWeapons)
            {
                Console.WriteLine($"AddBlockElements {type} 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField {type}[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField {type}[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField {type}[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            type = "SandboxVehicles";
            Console.WriteLine($"RemoveBlockElements {type} 0 *");
            foreach (var a in instance.SandboxVehicles)
            {
                Console.WriteLine($"AddBlockElements {type} 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField {type}[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField {type}[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField {type}[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            type = "SandboxScenery";
            Console.WriteLine($"RemoveBlockElements {type} 0 *");
            foreach (var a in instance.SandboxScenery)
            {
                Console.WriteLine($"AddBlockElements {type} 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField {type}[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField {type}[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField {type}[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            type = "SandboxSpawning";
            Console.WriteLine($"RemoveBlockElements {type} 0 *");
            foreach (var a in instance.SandboxSpawning)
            {
                Console.WriteLine($"AddBlockElements {type} 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField {type}[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField {type}[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField {type}[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            type = "SandboxTeleporters";
            Console.WriteLine($"RemoveBlockElements {type} 0 *");
            foreach (var a in instance.SandboxTeleporters)
            {
                Console.WriteLine($"AddBlockElements {type} 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField {type}[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField {type}[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField {type}[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            type = "SandboxGoalObjects";
            Console.WriteLine($"RemoveBlockElements {type} 0 *");
            foreach (var a in instance.SandboxGoalObjects)
            {
                Console.WriteLine($"AddBlockElements {type} 1");
                if (CacheContext.TagNames.ContainsKey(a.Object.Index))
                    Console.WriteLine($"SetField {type}[*].Object {CacheContext.TagNames[a.Object.Index]}.{a.Object.Group}");
                else
                    Console.WriteLine($"SetField {type}[*].Object 0x{a.Object.Index:X4}");

                Console.WriteLine($"SetField {type}[*].Name {CacheContext.StringIdCache.GetString(a.Name)}");

                Console.WriteLine("");
            }

            return true;
        }

        public bool DumpCommandsScript(List<string> args)
        {
            // Role: extract all the tags of a mode or sbsp tag.
            // Extract all the shaders of that tag, rmt2, vtsh, pixl and bitmaps of all the shaders
            // Dump commands to make a mod out of it.
            // Dump commands to reimport into a new build.

            // rmdf, rmt2, vtsh, pixl, mode, shader tags NEED to be named.

            if (args.Count != 1 || !CacheContext.TryGetTag(args[0], out var instance))
                return false;

            string modName = args[0].Split("\\".ToCharArray()).Last();

            if (!instance.IsInGroup("mode"))
                throw new NotImplementedException();

            IEnumerable<CachedTagInstance> dependencies = CacheContext.TagCache.Index.FindDependencies(instance);

            List<string> commands = new List<string>();

            // Console.WriteLine("All deps:");
            foreach (var dep in dependencies)
            {
                // To avoid porting a ton of existing textures, bitmaps under 0x5726 should be ignored

                // For stability and first runs, extract all. Filter out potentially existing tags later.
                // if (dep.Group.ToString() == "bitm" && dep.Index < 0x5726)
                // {
                //     // Ignore default bitmaps for now
                // }

                // These are common for all the shaders, so chances are small to see they get removed.
                if (dep.Group.Tag == "rmdf" || dep.Group.Tag == "rmop" || dep.Group.Tag == "glps" || dep.Group.Tag == "glvs")
                    continue;

                string depname = CacheContext.TagNames.ContainsKey(dep.Index) ? CacheContext.TagNames[dep.Index] : $"0x{dep.Index:X4}";
                string exportedTagName = $"{dep.Index:X4}";

                // if (!CacheContext.TagNames.ContainsKey(dep.Index))
                //     throw new Exception($"0x{dep.Index:X4} isn't named.");

                Console.WriteLine($"extracttag 0x{dep.Index:X4} {exportedTagName}.{dep.Group.Tag}");

                commands.Add($"createtag cfgt");
                commands.Add($"NameTag * {depname}");
                commands.Add($"importtag * {exportedTagName}.{dep.Group.Tag}");

                // Console.WriteLine($"createtag cfgt");
                // Console.WriteLine($"NameTag * {depname}");
                // Console.WriteLine($"importtag * {exportedTagName}.{dep.Group.Tag}");

                // Console.WriteLine($"Echo If the program quits at this point, the tagname is invalid.");
                // Console.WriteLine($"EditTag {depname}.{dep.Group.Tag}");
                // Console.WriteLine($"Exit");
                // Console.WriteLine($"Dumplog {modName}.log");
            }

            Console.WriteLine("");
            foreach (var a in commands)
                Console.WriteLine(a);

            RenderModel modeTag;
            using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
            {
                var edContext = new TagSerializationContext(cacheStream, CacheContext, instance);
                modeTag = CacheContext.Deserializer.Deserialize<RenderModel>(edContext);
            }

            var modename = CacheContext.TagNames[instance.Index];

            List<CachedTagInstance> shadersList = new List<CachedTagInstance>();

            Console.WriteLine("");

            Console.WriteLine($"EditTag {modename}.{instance.Group.Tag}");

            int i = -1;
            foreach (var material in modeTag.Materials)
            {
                i++;
                var shadername = CacheContext.TagNames[material.RenderMethod.Index];
                Console.WriteLine($"SetField Materials[{i}].RenderMethod {shadername}.{material.RenderMethod.Group.Tag}");

                shadersList.Add(material.RenderMethod);
            }

            Console.WriteLine($"SaveTagChanges");
            Console.WriteLine($"ExitTo tags");

            foreach (var shaderInstance in shadersList)
            {
                ShaderDecal shaderTag;
                using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, shaderInstance);
                    shaderTag = CacheContext.Deserializer.Deserialize<ShaderDecal>(edContext);
                }

                var shaderName = CacheContext.TagNames[shaderInstance.Index];
                var rmdfName = CacheContext.TagNames.ContainsKey(shaderTag.BaseRenderMethod.Index) ? CacheContext.TagNames[shaderTag.BaseRenderMethod.Index] : $"0x{shaderTag.BaseRenderMethod.Index:X4}";
                var rmt2Name = CacheContext.TagNames[shaderTag.ShaderProperties[0].Template.Index];

                // Manage rmt2
                RenderMethodTemplate rmt2Tag;
                using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, shaderTag.ShaderProperties[0].Template);
                    rmt2Tag = CacheContext.Deserializer.Deserialize<RenderMethodTemplate>(edContext);
                }

                var vtshName = CacheContext.TagNames[rmt2Tag.VertexShader.Index];
                var pixlName = CacheContext.TagNames[rmt2Tag.PixelShader.Index];

                Console.WriteLine("");
                Console.WriteLine($"EditTag {rmt2Name}.rmt2");
                Console.WriteLine($"SetField VertexShader {vtshName}.vtsh");
                Console.WriteLine($"SetField PixelShader {pixlName}.pixl");
                Console.WriteLine($"SaveTagChanges");
                Console.WriteLine($"ExitTo tags");

                // Manage bitmaps
                int j = -1;

                Console.WriteLine("");
                Console.WriteLine($"EditTag {shaderName}.{shaderInstance.Group.Tag}");
                Console.WriteLine($"SetField BaseRenderMethod {rmdfName}.rmdf");
                Console.WriteLine($"SetField ShaderProperties[0].Template {rmt2Name}.rmt2");
                foreach (var a in shaderTag.ShaderProperties[0].ShaderMaps)
                {
                    j++;
                    var bitmapName = CacheContext.TagNames.ContainsKey(a.Bitmap.Index) ? CacheContext.TagNames[a.Bitmap.Index] : $"0x{a.Bitmap.Index:X4}";
                    Console.WriteLine($"SetField ShaderProperties[0].ShaderMaps[{j}].Bitmap {bitmapName}.bitm");
                }
                Console.WriteLine($"SaveTagChanges");
                Console.WriteLine($"ExitTo tags");
            }

            Console.WriteLine("");
            Console.WriteLine($"SaveTagNames");
            Console.WriteLine($"Dumplog {modName}.log");
            Console.WriteLine($"Exit");

            return true;
        }

        public bool Temp(List<string> args)
        {
            var tags = CacheContext.TagCache.Index.FindAllInGroup("rmt2");

            foreach (var tag in tags)
            {
                RenderMethodTemplate edRmt2;
                using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, tag);
                    edRmt2 = CacheContext.Deserializer.Deserialize<RenderMethodTemplate>(edContext);
                }

                Console.WriteLine($"A:{edRmt2.Arguments.Count:D2} S:{edRmt2.ShaderMaps.Count:D2} 0x{tag.Index:X4} ");
            }

            return true;
        }

        public bool ShadowFix(List<string> args)
        {
            using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
            {
                if (!CacheContext.TryGetTag<Model>(args[0], out var hlmtInstance))
                {
                    Console.WriteLine($"ERROR: tag group must be 'hlmt'. Supplied tag group was '{hlmtInstance.Group.Tag}'.");
                    return false;
                }

                var edContext = new TagSerializationContext(cacheStream, CacheContext, hlmtInstance);
                var hlmtDefinition = CacheContext.Deserializer.Deserialize<Model>(edContext);

                hlmtDefinition.CollisionRegions.Add(
                    new Model.CollisionRegion
                    {
                        Permutations = new List<Model.CollisionRegion.Permutation>
                        {
                            new Model.CollisionRegion.Permutation()
                        }
                    });

                edContext = new TagSerializationContext(cacheStream, CacheContext, hlmtInstance);
                CacheContext.Serializer.Serialize(edContext, hlmtDefinition);

                edContext = new TagSerializationContext(cacheStream, CacheContext, hlmtDefinition.RenderModel);
                var modeDefinition = CacheContext.Deserializer.Deserialize<RenderModel>(edContext);

                var resourceContext = new ResourceSerializationContext(modeDefinition.Geometry.Resource);
                var geometryResource = CacheContext.Deserializer.Deserialize<RenderGeometryApiResourceDefinition>(resourceContext);

                geometryResource.IndexBuffers.Add(new D3DPointer<IndexBufferDefinition>
                {
                    Address = 0,
                    UnusedC = 0,
                    Definition = new IndexBufferDefinition
                    {
                        Format = IndexBufferFormat.TriangleStrip,
                        Data = new TagData
                        {
                            Size = 0x6,
                            Address = geometryResource.IndexBuffers[0].Definition.Data.Address
                        }
                    }
                });

                geometryResource.VertexBuffers.Add(new D3DPointer<VertexBufferDefinition>
                {
                    Definition = new VertexBufferDefinition
                    {
                        Count = 3,
                        VertexSize = 0x38,
                        Data = new TagData
                        {
                            Size = 0xA8,
                            Address = geometryResource.VertexBuffers[0].Definition.Data.Address
                        }
                    }
                });

                geometryResource.VertexBuffers.Add(new D3DPointer<VertexBufferDefinition>
                {
                    Definition = new VertexBufferDefinition
                    {
                        Count = 3,
                        VertexSize = 0x38,
                        Data = new TagData
                        {
                            Size = 0xA8,
                            Address = geometryResource.VertexBuffers[1].Definition.Data.Address
                        }
                    }
                });

                CacheContext.Serializer.Serialize(resourceContext, geometryResource);

                modeDefinition.Geometry.Meshes.Add(new Mesh
                {
                    VertexBufferIndices = new ushort[] { (ushort)(geometryResource.VertexBuffers.Count - 2), 0xFFFF, 0xFFFF, (ushort)(geometryResource.VertexBuffers.Count - 1), 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF },
                    IndexBufferIndices = new ushort[] { (ushort)(geometryResource.IndexBuffers.Count - 1), 0xFFFF },
                    Type = VertexType.Rigid,
                    PrtType = PrtType.Ambient,
                    IndexBufferType = PrimitiveType.TriangleStrip,
                    RigidNodeIndex = 0,
                    Parts = new List<Mesh.Part>
                    {
                        new Mesh.Part
                        {
                            TransparentSortingIndex = -1,
                            SubPartCount = 1,
                            TypeNew = Mesh.Part.PartTypeNew.OpaqueShadowCasting,
                            FlagsNew = Mesh.Part.PartFlagsNew.PerVertexLightmapPart,
                            VertexCount = 3
                        },
                    },
                    SubParts = new List<Mesh.SubPart>
                    {
                        new Mesh.SubPart
                        {
                            FirstIndex = 0,
                            IndexCount = 3,
                            PartIndex = 0,
                            VertexCount = 0
                        }
                    }
                });

                modeDefinition.Regions.Add(
                    new RenderModel.Region
                    {
                        Permutations = new List<RenderModel.Region.Permutation>
                        {
                            new RenderModel.Region.Permutation
                            {
                                MeshIndex = (short)(modeDefinition.Geometry.Meshes.Count - 1)
                            }
                        }
                    });

                edContext = new TagSerializationContext(cacheStream, CacheContext, hlmtDefinition.RenderModel);
                CacheContext.Serializer.Serialize(edContext, modeDefinition);
            }

            return true;
        }

        [TagStructure(Name = "render_method", Tag = "rm  ", Size = 0x20)]
        public class RenderMethodFast
        {
            public CachedTagInstance BaseRenderMethod;
            public List<RenderMethod.UnknownBlock> Unknown;
        }

        public bool NameRmt2()
        {
            var validShaders = new List<string> { "rmsh", "rmtr", "rmhg", "rmfl", "rmcs", "rmss", "rmd ", "rmw ", "rmzo", "ltvl", "prt3", "beam", "decs", "cntl", "rmzo", "rmct", "rmbk" };
            var newlyNamedRmt2 = new List<int>();
            var type = "invalid";
            var rmt2Instance = -1;

            // Unname rmt2 tags
            foreach (var edInstance in CacheContext.TagCache.Index.FindAllInGroup("rmt2"))
                CacheContext.TagNames[edInstance.Index] = "blank";

            foreach (var edInstance in CacheContext.TagCache.Index.NonNull())
            {
                object rm = null;
                RenderMethod renderMethod = null;

                // ignore tag groups not in validShaders
                if (!validShaders.Contains(edInstance.Group.Tag.ToString()))
                    continue;

                // Console.WriteLine($"Checking 0x{edInstance:x4} {edInstance.Group.Tag.ToString()}");

                // Get the tagname type per tag group
                switch (edInstance.Group.Tag.ToString())
                {
                    case "rmsh": type = "shader"; break;
                    case "rmtr": type = "terrain"; break;
                    case "rmhg": type = "halogram"; break;
                    case "rmfl": type = "foliage"; break;
                    case "rmss": type = "screen"; break;
                    case "rmcs": type = "custom"; break;
                    case "prt3": type = "particle"; break;
                    case "beam": type = "beam"; break;
                    case "cntl": type = "contrail"; break;
                    case "decs": type = "decal"; break;
                    case "ltvl": type = "light_volume"; break;
                    case "rmct": type = "cortana"; break;
                    case "rmbk": type = "black"; break;
                    case "rmzo": type = "zonly"; break;
                    case "rmd ": type = "decal"; break;
                    case "rmw ": type = "water"; break;
                }

                switch (edInstance.Group.Tag.ToString())
                {
                    case "rmsh":
                    case "rmhg":
                    case "rmtr":
                    case "rmcs":
                    case "rmfl":
                    case "rmss":
                    case "rmct":
                    case "rmzo":
                    case "rmbk":
                    case "rmd ":
                    case "rmw ":
                        using (var cacheStream = CacheContext.TagCacheFile.Open(FileMode.Open, FileAccess.ReadWrite))
                        using (var cacheReader = new EndianReader(cacheStream))
                        {
                            var edContext = new TagSerializationContext(cacheStream, CacheContext, edInstance);
                            var edDefinition = CacheContext.Deserializer.Deserialize<RenderMethodFast>(new TagSerializationContext(cacheStream, CacheContext, edInstance));

                            if (edDefinition.Unknown == null || edDefinition.Unknown.Count == 0)
                                continue;

                            renderMethod = new RenderMethod
                            {
                                Unknown = edDefinition.Unknown
                            };
                        }

                        foreach (var a in edInstance.Dependencies)
                            if (CacheContext.GetTag(a).Group.ToString() == "rmt2")
                                rmt2Instance = CacheContext.GetTag(a).Index;

                        if (rmt2Instance == 0)
                            throw new Exception();

                        NameRmt2Part(type, renderMethod, edInstance, rmt2Instance, newlyNamedRmt2);
                        continue;

                    default:
                        break;
                }

                using (var cacheStream = CacheContext.TagCacheFile.Open(FileMode.Open, FileAccess.ReadWrite))
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, edInstance);
                    rm = CacheContext.Deserializer.Deserialize(new TagSerializationContext(cacheStream, CacheContext, edInstance), TagDefinition.Find(edInstance.Group.Tag));
                }

                switch (edInstance.Group.Tag.ToString())
                {
                    case "prt3": var e = (Particle)rm; NameRmt2Part(type, e.RenderMethod, edInstance, e.RenderMethod.ShaderProperties[0].Template.Index, newlyNamedRmt2); break;
                    case "beam": var a = (BeamSystem)rm; foreach (var f in a.Beam) NameRmt2Part(type, f.RenderMethod, edInstance, f.RenderMethod.ShaderProperties[0].Template.Index, newlyNamedRmt2); break;
                    case "cntl": var b = (ContrailSystem)rm; foreach (var f in b.Contrail) NameRmt2Part(type, f.RenderMethod, edInstance, f.RenderMethod.ShaderProperties[0].Template.Index, newlyNamedRmt2); break;
                    case "decs": var c = (DecalSystem)rm; foreach (var f in c.Decal) NameRmt2Part(type, f.RenderMethod, edInstance, f.RenderMethod.ShaderProperties[0].Template.Index, newlyNamedRmt2); break;
                    case "ltvl": var d = (LightVolumeSystem)rm; foreach (var f in d.LightVolume) NameRmt2Part(type, f.RenderMethod, edInstance, f.RenderMethod.ShaderProperties[0].Template.Index, newlyNamedRmt2); break;

                    default:
                        break;
                }
            }


            return true;
        }

        private void NameRmt2Part(string type, RenderMethod renderMethod, CachedTagInstance edInstance, int rmt2Instance, List<int> newlyNamedRmt2)
        {
            if (renderMethod.Unknown.Count == 0) // invalid shaders, most likely caused by ported shaders
                return;

            if (newlyNamedRmt2.Contains(rmt2Instance))
                return;
            else
                newlyNamedRmt2.Add(rmt2Instance);

            var newTagName = $"shaders\\{type}_templates\\";

            var rmdfRefValues = "";

            for (int i = 0; i < renderMethod.Unknown.Count; i++)
            {
                if (edInstance.Group.Tag.ToString() == "rmsh" && i > 9) // for better H3/ODST name matching
                    break;

                if (edInstance.Group.Tag.ToString() == "rmhg" && i > 6) // for better H3/ODST name matching
                    break;

                rmdfRefValues = $"{rmdfRefValues}_{renderMethod.Unknown[i].Unknown}";
            }

            newTagName = $"{newTagName}{rmdfRefValues}";

            CacheContext.TagNames[rmt2Instance] = newTagName;
            // Console.WriteLine($"0x{rmt2Instance:X4} {newTagName}");
        }

        public bool AdjustScripts(List<string> args)
        {
            var helpMessage =
                @"Usage: " +
                @"test AdjustScripts levels\multi\guardian\guardian";

            if (args.Count != 1)
            {
                Console.WriteLine(helpMessage);
                Console.WriteLine("args.Count != 1");
                return false;
            }

            var edTagArg = args[0];

            if (!CacheContext.TryGetTag(edTagArg, out var edTag))
            {
                Console.WriteLine($"ERROR: cannot find tag {edTag}");
                Console.WriteLine(helpMessage);
                return false;
            }

            if (!edTag.IsInGroup("scnr"))
            {
                Console.WriteLine($"ERROR: tag is not a scenario {edTag}");
                Console.WriteLine(helpMessage);
                return false;
            }

            if (!CacheContext.TagNames.ContainsKey(edTag.Index))
            {
                Console.WriteLine($"CacheContext.TagNames.ContainsKey(edTag.Index) {edTag.Index:X4}");
                return false;
            }

            var tagName = CacheContext.TagNames[edTag.Index].Split("\\".ToCharArray()).Last();

            if (!DisabledScriptsString.ContainsKey(tagName))
            {
                Console.WriteLine("!DisabledScriptsString.ContainsKey(tagName)");
                return false;
            }

            Scenario scnr;
            using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
            {
                var edContext = new TagSerializationContext(cacheStream, CacheContext, edTag);
                scnr = CacheContext.Deserializer.Deserialize<Scenario>(edContext);
            }

            foreach (var line in DisabledScriptsString[tagName])
            {
                var items = line.Split(",".ToCharArray());

                var scriptIndex = Convert.ToInt32(items[0]);

                uint.TryParse(items[2], NumberStyles.HexNumber, null, out uint NextExpressionHandle);
                ushort.TryParse(items[3], NumberStyles.HexNumber, null, out ushort Opcode);
                byte.TryParse(items[4].Substring(0, 2), NumberStyles.HexNumber, null, out byte data0);
                byte.TryParse(items[4].Substring(2, 2), NumberStyles.HexNumber, null, out byte data1);
                byte.TryParse(items[4].Substring(4, 2), NumberStyles.HexNumber, null, out byte data2);
                byte.TryParse(items[4].Substring(6, 2), NumberStyles.HexNumber, null, out byte data3);

                scnr.ScriptExpressions[scriptIndex].NextExpressionHandle = NextExpressionHandle;
                scnr.ScriptExpressions[scriptIndex].Opcode = Opcode;
                scnr.ScriptExpressions[scriptIndex].Data[0] = data0;
                scnr.ScriptExpressions[scriptIndex].Data[1] = data1;
                scnr.ScriptExpressions[scriptIndex].Data[2] = data2;
                scnr.ScriptExpressions[scriptIndex].Data[3] = data3;
            }

            using (var stream = CacheContext.TagCacheFile.Open(FileMode.Open, FileAccess.ReadWrite))
            {
                var context = new TagSerializationContext(stream, CacheContext, edTag);
                CacheContext.Serializer.Serialize(context, scnr);
            }

            return true;

        }

        public bool AdjustScriptsFromFile(List<string> args)
        {
            var helpMessage =
                @"Usage: " +
                @"test AdjustScripts levels\multi\guardian\guardian guardian.csv";

            if (args.Count != 2)
            {
                Console.WriteLine("ERROR: args.Count != 2");
                Console.WriteLine(helpMessage);
                return false;
            }

            var edTagArg = args[0];
            var file = args[1];

            if (!CacheContext.TryGetTag(edTagArg, out var edTag))
            {
                Console.WriteLine($"ERROR: cannot find tag {edTag}");
                Console.WriteLine(helpMessage);
                return false;
            }

            if (!edTag.IsInGroup("scnr"))
            {
                Console.WriteLine($"ERROR: tag is not a scenario {edTag}");
                Console.WriteLine(helpMessage);
                return false;
            }

            var file_ = new FileInfo(file);

            if (!File.Exists(file))
            {
                Console.WriteLine($"ERROR: file does not exist: {file}");
                Console.WriteLine(helpMessage);
                return false;
            }

            Scenario scnr;
            using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
            {
                var edContext = new TagSerializationContext(cacheStream, CacheContext, edTag);
                scnr = CacheContext.Deserializer.Deserialize<Scenario>(edContext);
            }

            var lines = new List<string>();

            using (var csvStream = file_.OpenRead())
            using (var csvReader = new StreamReader(csvStream))
            {
                var line = "";
                while (line != null)
                {
                    line = csvReader.ReadLine();

                    if (line == null)
                        break;

                    if (line == "// STOP")
                        break;

                    if (line.StartsWith("//"))
                        continue;

                    if (line == "")
                        continue;

                    lines.Add(line);
                }
            }

            foreach (var line in lines)
            {
                var items = line.Split(",".ToCharArray());

                var scriptIndex = Convert.ToInt32(items[0]);

                uint.TryParse(items[2], NumberStyles.HexNumber, null, out uint NextExpressionHandle);
                ushort.TryParse(items[3], NumberStyles.HexNumber, null, out ushort Opcode);
                byte.TryParse(items[4].Substring(0, 2), NumberStyles.HexNumber, null, out byte data0);
                byte.TryParse(items[4].Substring(2, 2), NumberStyles.HexNumber, null, out byte data1);
                byte.TryParse(items[4].Substring(4, 2), NumberStyles.HexNumber, null, out byte data2);
                byte.TryParse(items[4].Substring(6, 2), NumberStyles.HexNumber, null, out byte data3);

                scnr.ScriptExpressions[scriptIndex].NextExpressionHandle = NextExpressionHandle;
                scnr.ScriptExpressions[scriptIndex].Opcode = Opcode;
                scnr.ScriptExpressions[scriptIndex].Data[0] = data0;
                scnr.ScriptExpressions[scriptIndex].Data[1] = data1;
                scnr.ScriptExpressions[scriptIndex].Data[2] = data2;
                scnr.ScriptExpressions[scriptIndex].Data[3] = data3;
            }

            using (var stream = CacheContext.TagCacheFile.Open(FileMode.Open, FileAccess.ReadWrite))
            {
                var context = new TagSerializationContext(stream, CacheContext, edTag);
                CacheContext.Serializer.Serialize(context, scnr);
            }

            return true;
        }

        public bool BatchTagDepAdd(List<string> args)
        {
            var helpMessage =
                "Usage: " +
                "test BatchTagDepAdd 0x0 0x1234 0x4567 rmsh" +
                "test BatchTagDepAdd <main tag> <first tag dep> <last tag dep> <tag class>" +
                "Add new tag dependencies to the first specified tag. Add all the tags between the second and the last specified tags.";

            if (args.Count != 4)
            {
                Console.WriteLine(helpMessage);
                Console.WriteLine("args.Count != 4");
                return false;
            }

            var tag1arg = args[0];
            var tag2arg = args[1];
            var tag3arg = args[2];
            var tagClas = args[3];

            if (!CacheContext.TryGetTag(tag1arg, out var tag1))
            {
                Console.WriteLine($"ERROR: cannot find tag {tag1}");
                Console.WriteLine(helpMessage);
                return false;
            }

            if (!CacheContext.TryGetTag(tag2arg, out var tag2))
            {
                Console.WriteLine($"ERROR: cannot find tag {tag2}");
                Console.WriteLine(helpMessage);
                return false;
            }

            if (!CacheContext.TryGetTag(tag3arg, out var tag3))
            {
                Console.WriteLine($"ERROR: cannot find tag {tag3}");
                Console.WriteLine(helpMessage);
                return false;
            }

            var dependencies = new List<int>();

            // foreach (var tag in CacheContext.TagCache.Index.FindAllInGroup(args[4]))
            foreach (var tag in CacheContext.TagCache.Index.FindAllInGroup(tagClas))
            {
                if (tag.Index < tag2.Index)
                    continue;

                if (tag.Index > tag3.Index)
                    break;

                dependencies.Add(tag.Index);
            }

            // Based on TagDependencyCommand
            using (var stream = CacheContext.OpenTagCacheReadWrite())
            {
                var data = CacheContext.TagCache.ExtractTag(stream, tag1);

                foreach (var dependency in dependencies)
                {
                    if (data.Dependencies.Add(dependency))
                        Console.WriteLine("Added dependency on tag {0:X8}.", dependency);
                    else
                        Console.Error.WriteLine("Tag {0:X8} already depends on tag {1:X8}.", tag1.Index, dependency);
                }

                CacheContext.TagCache.SetTagData(stream, tag1, data);
            }

            return true;
        }

        private static Dictionary<string, List<string>> DisabledScriptsString = new Dictionary<string, List<string>>
        {
            ["005_intro"] = new List<string>
            {
                // default scripts:
                "00000308,E4A70134,E4A90136,0376,3501A8E4,Group,Void,cinematic_skip_stop_internal,",
                
                // modified scripts:
                "00003019,EF3E0BCB,EF440BD1,0424,CC0B3FEF,Group,Void,chud_show_shield,",
                "00000319,E4B2013F,FFFFFFFF,0000,00000000,Expression,FunctionName,begin,",
                "00002221,EC2008AD,EC2F08BC,0053,AE0821EC,ScriptReference,Void,",
            },
        };

        public static List<string> Halo3MPCommonCacheFiles = new List<string> {
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\guardian.map"   ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\riverworld.map" ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\bunkerworld.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\chill.map"      ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\cyberdyne.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\deadlock.map"   ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\shrine.map"     ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\zanzibar.map"   ,
        };

        public static List<string> Halo3MPUncommonCacheFiles = new List<string> {
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\armory.map"     ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\chillout.map"   ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\construct.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\descent.map"    ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\docks.map"      ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\fortress.map"   ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\ghosttown.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\isolation.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\lockout.map"    ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\midship.map"    ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\salvation.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\sandbox.map"    ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\sidewinder.map" ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\snowbound.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\spacecamp.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Mythic\maps\warehouse.map"  ,
        };

        public static List<string> Halo3CampaignCacheFiles = new List<string> {
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\005_intro.map"    ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\010_jungle.map"   ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\020_base.map"     ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\030_outskirts.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\040_voi.map"      ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\050_floodvoi.map" ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\070_waste.map"    ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\100_citadel.map"  ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\110_hc.map"       ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\120_halo.map"     ,
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\130_epilogue.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3Campaign\maps\mainmenu.map"   ,
        };

        public static List<string> Halo3ODSTCacheFiles = new List<string> {
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\mainmenu.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\c100.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\c200.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\h100.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\l200.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\l300.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\sc100.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\sc110.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\sc120.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\sc130.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\sc140.map",
            @"D:\FOLDERS\Xenia\ISO\Halo3ODST\maps\sc150.map",
        };

        private bool IsDiffOrNull(CachedTagInstance a, CachedTagInstance b, CacheFile BlamCache)
        {
            // a is ed tag, b is blam tag
            if ((a != null && b == null) ||
                (a == null && b != null))
                return false;

            if (a == null && b == null)
                return true;

            CacheContext.TagNames[a.Index] = BlamCache.IndexItems.GetItemByID(b.Index).Name;

            return true;
        }

        private class Item
        {
            public int TagIndex;
            public string Tagname;
            public string ModeName;
            public uint Checksum;
        }

        [TagStructure(Name = "render_model", Tag = "mode", Size = 0x1CC, MaxVersion = CacheVersion.Halo3ODST)]
        [TagStructure(Name = "render_model", Tag = "mode", Size = 0x1D0, MinVersion = CacheVersion.HaloOnline106708)]
        public class RenderModel_materials
        {
            public StringId Name;
            public int Padding0;
            public int Padding1;
            public int Padding2;
            public int Padding3;
            public int Padding4;
            public int Padding5;
            public int Padding6;
            public int Padding7;
            public int Padding8;
            public int Padding9;
            public int PaddingA;
            public int PaddingB;
            public int PaddingC;
            public int PaddingD;
            public int PaddingE;
            public int PaddingF;
            public int Padding10;
            public List<RenderMaterial> Materials;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public string GetCurrentMethod()
        {
            StackTrace st = new StackTrace();
            StackFrame sf = st.GetFrame(1);

            return sf.GetMethod().Name;
        }

        public bool NameGlobalMaterials()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            var edAlreadyNamedTag = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);
                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.Name != "globals\\globals")
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmGlobals = blamDeserializer.Deserialize<Globals>(blamContext);

                        if (!CacheContext.TryGetTag($"globals\\globals.matg", out var edInstance))
                            throw new Exception();

                        if (edInstance == null)
                            throw new Exception();

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, edInstance);
                            var edGlobals = CacheContext.Deserialize<Globals>(tagContext);

                            for (int i = 0; i < bmGlobals.Materials.Count; i++)
                            {
                                if (i >= edGlobals.Materials.Count)
                                    continue;

                                IsDiffOrNull(edGlobals.Materials[i].BreakableSurface, bmGlobals.Materials[i].BreakableSurface, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].EffectSweetenerGrinding, bmGlobals.Materials[i].EffectSweetenerGrinding, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].EffectSweetenerLarge, bmGlobals.Materials[i].EffectSweetenerLarge, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].EffectSweetenerMedium, bmGlobals.Materials[i].EffectSweetenerMedium, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].EffectSweetenerMelee, bmGlobals.Materials[i].EffectSweetenerMelee, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].EffectSweetenerRolling, bmGlobals.Materials[i].EffectSweetenerRolling, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].EffectSweetenerSmall, bmGlobals.Materials[i].EffectSweetenerSmall, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].MaterialEffects, bmGlobals.Materials[i].MaterialEffects, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerGrinding, bmGlobals.Materials[i].SoundSweetenerGrinding, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerLarge, bmGlobals.Materials[i].SoundSweetenerLarge, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerMedium, bmGlobals.Materials[i].SoundSweetenerMedium, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerMeleeLarge, bmGlobals.Materials[i].SoundSweetenerMeleeLarge, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerMeleeMedium, bmGlobals.Materials[i].SoundSweetenerMeleeMedium, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerMeleeSmall, bmGlobals.Materials[i].SoundSweetenerMeleeSmall, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerRolling, bmGlobals.Materials[i].SoundSweetenerRolling, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].SoundSweetenerSmall, bmGlobals.Materials[i].SoundSweetenerSmall, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].WaterRippleLarge, bmGlobals.Materials[i].WaterRippleLarge, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].WaterRippleMedium, bmGlobals.Materials[i].WaterRippleMedium, BlamCache);
                                IsDiffOrNull(edGlobals.Materials[i].WaterRippleSmall, bmGlobals.Materials[i].WaterRippleSmall, BlamCache);
                            }
                        }

                        break;
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameBlocSubtags()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "bloc")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
    item.GroupName == "user_interface_fourth_wall_timing_definition" ||
    item.GroupName == "scenario_pda")
                            continue;

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (!CacheContext.TryGetTag(tagname, out var tag1))
                            continue;

                        if (tag1 == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var crateBm = blamDeserializer.Deserialize<Crate>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag1);
                            var crateED = CacheContext.Deserialize<Crate>(tagContext);

                            if (crateED.Attachments.Count != crateBm.Attachments.Count)
                                continue;

                            IsDiffOrNull(crateED.MaterialEffects, crateBm.MaterialEffects, BlamCache);

                            for (int i = 0; i < crateED.Attachments.Count; i++)
                                IsDiffOrNull(crateED.Attachments[i].Attachment2, crateBm.Attachments[i].Attachment2, BlamCache);
                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameEffe()
        {
            return false;

            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "effe")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
    item.GroupName == "user_interface_fourth_wall_timing_definition" ||
    item.GroupName == "scenario_pda")
                            continue;

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (item.GroupName == "hlsl_include")
                            continue;

                        if (item.GroupName == "user_interface_fourth_wall_timing_definition")
                            continue;

                        if (item.GroupName == "scenario_pda")
                            continue;

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (item.Name.Contains("."))
                            continue;

                        if (!CacheContext.TryGetTag(tagname, out var tag1))
                            continue;

                        if (tag1 == null)
                            continue;

                        //Csv1($"{item.GroupName},{cacheFile},{tagname}");

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var crateBm = blamDeserializer.Deserialize<Effect>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag1);
                            var crateED = CacheContext.Deserialize<Effect>(tagContext);
                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameEffeSubtags()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "effe")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
    item.GroupName == "user_interface_fourth_wall_timing_definition" ||
    item.GroupName == "scenario_pda")

                            continue;
                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (!CacheContext.TryGetTag(tagname, out var tag1))
                            continue;

                        if (tag1 == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmTag = blamDeserializer.Deserialize<Effect>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag1);
                            var edTag = CacheContext.Deserialize<Effect>(tagContext);

                            for (int i = 0; i < edTag.Events.Count; i++)
                            {
                                if (edTag.Events.Count != bmTag.Events.Count)
                                    continue;

                                for (int j = 0; j < edTag.Events[i].Parts.Count; j++)
                                {
                                    if (edTag.Events[i].Parts.Count != bmTag.Events[i].Parts.Count)
                                        continue;

                                    if (!IsDiffOrNull(edTag.Events[i].Parts[j].Type, bmTag.Events[i].Parts[j].Type, BlamCache))
                                        continue;
                                }

                                for (int j = 0; j < edTag.Events[i].ParticleSystems.Count; j++)
                                {
                                    if (edTag.Events[i].ParticleSystems.Count != bmTag.Events[i].ParticleSystems.Count)
                                        continue;

                                    if (!IsDiffOrNull(edTag.Events[i].ParticleSystems[j].Particle, bmTag.Events[i].ParticleSystems[j].Particle, BlamCache))
                                        continue;

                                    for (int k = 0; k < edTag.Events[i].ParticleSystems[j].Emitters.Count; k++)
                                        if (!IsDiffOrNull(edTag.Events[i].ParticleSystems[j].Emitters[k].ParticleMovement.Template, bmTag.Events[i].ParticleSystems[j].Emitters[k].ParticleMovement.Template, BlamCache))
                                            continue;
                                }
                            }
                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameModeTags()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var blamTags = new List<Item>();
            var edTags = new List<Item>();
            var list = new List<string>();
            debugConsoleWrite = false;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "mode")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
    item.GroupName == "user_interface_fourth_wall_timing_definition" ||
    item.GroupName == "scenario_pda")
                            continue;

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var tag = blamDeserializer.Deserialize<RasterizerCacheFileGlobals>(blamContext);

                        var modeName = BlamCache.Strings.GetItemByID(new StringId(tag.Unknown).Index);

                        switch (modeName)
                        {
                            case "sky":
                            case "fp":
                            case "fp_body":
                                continue;
                            default:
                                break;
                        }

                        blamTags.Add(new Item
                        {
                            Tagname = item.Name,
                            ModeName = modeName,
                            Checksum = tag.Unknown3
                        });
                    }
                }
            }

            foreach (var instance in CacheContext.TagCache.Index.FindAllInGroup("mode"))
            {
                RasterizerCacheFileGlobals tag;
                using (var cacheStream = CacheContext.OpenTagCacheReadWrite())
                {
                    var edContext = new TagSerializationContext(cacheStream, CacheContext, instance);
                    tag = CacheContext.Deserializer.Deserialize<RasterizerCacheFileGlobals>(edContext);
                }

                var modeName = CacheContext.StringIdCache.GetString(new StringId(tag.Unknown));

                switch (modeName)
                {
                    case "sky":
                    case "fp":
                    case "fp_body":
                        continue;
                    default:
                        break;
                }

                edTags.Add(new Item
                {
                    TagIndex = instance.Index,
                    ModeName = modeName,
                    Checksum = tag.Unknown3
                });
            }

            var edNamedTags = new List<Item>();

            foreach (var blamTag in blamTags)
            {
                foreach (var edTag in edTags)
                {
                    if (edTag.ModeName != blamTag.ModeName)
                        continue;

                    if (edNamedTags.Contains(edTag))
                        continue;

                    edNamedTags.Add(edTag);

                    CacheContext.TagNames[edTag.TagIndex] = blamTag.Tagname;

                    // if (!CacheContext.TagNames.ContainsKey(edTag.TagIndex))
                    // {
                    //     if (edTag.Checksum != blamTag.Checksum)
                    //         Csv1($"NameTag 0x{edTag.TagIndex:X4} {blamTag.Tagname},{edTag.Checksum:X8},{blamTag.Checksum:X8},diff check");
                    //     else
                    //         Csv1($"NameTag 0x{edTag.TagIndex:X4} {blamTag.Tagname},{edTag.Checksum:X8},{blamTag.Checksum:X8},same check");
                    //     goto FoundED;
                    // }
                }

                FoundED:
                ;
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameModeShaders()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            var edAlreadyNamedTag = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "mode")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
                            item.GroupName == "user_interface_fourth_wall_timing_definition" ||
                            item.GroupName == "scenario_pda")
                            continue;

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (item.Name.Contains("."))
                            continue;

                        if (!CacheContext.TryGetTag(tagname, out var edInstance))
                            continue;

                        if (edInstance == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmMode = blamDeserializer.Deserialize<RenderModel_materials>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, edInstance);
                            var edMode = CacheContext.Deserialize<RenderModel_materials>(tagContext);

                            if (bmMode.Materials.Count != edMode.Materials.Count)
                            {
                                //Csv2($"{item.Name},bmMode.Materials.Count != edMode.Materials.Count");
                                continue;
                            }

                            for (int i = 0; i < bmMode.Materials.Count; i++)
                                IsDiffOrNull(edMode.Materials[i].RenderMethod, bmMode.Materials[i].RenderMethod, BlamCache);
                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameGameObjectsSubtags()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        switch (item.GroupTag.ToString())
                        {
                            case "bloc":
                            case "efsc":
                            case "snce":
                            case "armr":
                            case "proj":
                            case "crea":
                            case "devi":
                            case "bipd":
                            case "vehi":
                            case "gint":
                            case "ssce":
                            case "scen":
                            case "item":
                            case "weap":
                            case "eqip":
                            case "term":
                            case "ctrl":
                            case "mach":
                                break;
                            default:
                                continue;
                        }

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (!CacheContext.TryGetTag(tagname, out var tag1))
                            continue;

                        if (tag1 == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmTag = blamDeserializer.Deserialize<EffectScenery>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag1);
                            var edTag = CacheContext.Deserialize<EffectScenery>(tagContext);

                            IsDiffOrNull(edTag.Model, bmTag.Model, BlamCache);
                            IsDiffOrNull(edTag.CrateObject, bmTag.CrateObject, BlamCache);
                            IsDiffOrNull(edTag.CollisionDamage, bmTag.CollisionDamage, BlamCache);
                            IsDiffOrNull(edTag.CreationEffect, bmTag.CreationEffect, BlamCache);
                            IsDiffOrNull(edTag.MaterialEffects, bmTag.MaterialEffects, BlamCache);
                            IsDiffOrNull(edTag.MeleeImpact, bmTag.MeleeImpact, BlamCache);
                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameModelSubtags()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        switch (item.GroupTag.ToString())
                        {
                            case "hlmt":
                                break;
                            default:
                                continue;
                        }

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (!CacheContext.TryGetTag(tagname, out var tag1))
                            continue;

                        if (tag1 == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmTag = blamDeserializer.Deserialize<Model>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, tag1);
                            var edTag = CacheContext.Deserialize<Model>(tagContext);

                            IsDiffOrNull(edTag.RenderModel, bmTag.RenderModel, BlamCache);
                            IsDiffOrNull(edTag.CollisionModel, bmTag.CollisionModel, BlamCache);
                            IsDiffOrNull(edTag.Animation, bmTag.Animation, BlamCache);
                            IsDiffOrNull(edTag.PhysicsModel, bmTag.PhysicsModel, BlamCache);
                            IsDiffOrNull(edTag.LodModel, bmTag.LodModel, BlamCache);
                            IsDiffOrNull(edTag.PrimaryDialogue, bmTag.PrimaryDialogue, BlamCache);
                            IsDiffOrNull(edTag.SecondaryDialogue, bmTag.SecondaryDialogue, BlamCache);
                            IsDiffOrNull(edTag.ShieldImpactFirstPerson, bmTag.ShieldImpactFirstPerson, BlamCache);
                            IsDiffOrNull(edTag.ShieldImpactThirdPerson, bmTag.ShieldImpactThirdPerson, BlamCache);
                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameFootSnd()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            var edAlreadyNamedTag = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "foot")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
                            item.GroupName == "user_interface_fourth_wall_timing_definition" ||
                            item.GroupName == "scenario_pda")
                            continue;

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (item.Name.Contains("."))
                            continue;

                        if (!CacheContext.TryGetTag(tagname, out var edInstance))
                            continue;

                        if (edInstance == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmMode = blamDeserializer.Deserialize<MaterialEffects>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, edInstance);
                            var edMode = CacheContext.Deserialize<MaterialEffects>(tagContext);

                            if (bmMode.Effects.Count != edMode.Effects.Count)
                            {
                                //Csv2($"{item.Name},bmMode.Materials.Count != edMode.Materials.Count");
                                continue;
                            }

                            for (int i = 0; i < bmMode.Effects.Count; i++)
                            {
                                if (bmMode.Effects[i].OldMaterials.Count != edMode.Effects[i].OldMaterials.Count)
                                {
                                    //Csv2($"{item.Name},bmMode.OldMaterials.Count != edMode.OldMaterials.Count");
                                    goto TagMismatch;
                                }

                                if (bmMode.Effects[i].Sounds.Count != edMode.Effects[i].Sounds.Count)
                                {
                                    //Csv2($"{item.Name},bmMode.Sounds.Count != edMode.Sounds.Count");
                                    goto TagMismatch;
                                }

                                if (bmMode.Effects[i].Effects.Count != edMode.Effects[i].Effects.Count)
                                {
                                    //Csv2($"{item.Name},bmMode.Effects.Count != edMode.Effects.Count");
                                    goto TagMismatch;
                                }
                            }

                            for (int i = 0; i < bmMode.Effects.Count; i++)
                            {
                                for (int j = 0; j < bmMode.Effects[i].Sounds.Count; j++)
                                    IsDiffOrNull(edMode.Effects[i].Sounds[j].Effect, bmMode.Effects[i].Sounds[j].Effect, BlamCache);
                                for (int j = 0; j < bmMode.Effects[i].Effects.Count; j++)
                                    IsDiffOrNull(edMode.Effects[i].Effects[j].Effect, bmMode.Effects[i].Effects[j].Effect, BlamCache);
                            }

                            TagMismatch:
                            ;
                        }
                    }

                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

        public bool NameLsndSubtags()
        {
            var haloMapsList = new List<List<string>> { Halo3MPCommonCacheFiles, Halo3MPUncommonCacheFiles, Halo3ODSTCacheFiles, Halo3CampaignCacheFiles };
            var list = new List<string>();
            var edAlreadyNamedTag = new List<string>();
            debugConsoleWrite = true;

            foreach (var cacheGroup in haloMapsList)
            {
                foreach (var cacheFile in cacheGroup)
                {
                    var BlamCache = OpenCacheFile(cacheFile);

                    var blamDeserializer = new TagDeserializer(BlamCache.Version);

                    foreach (var item in BlamCache.IndexItems)
                    {
                        if (item.ClassIndex == -1)
                            continue;

                        if (item.GroupTag != "lsnd")
                            continue;

                        if (item.GroupName == "hlsl_include" ||
                            item.GroupName == "user_interface_fourth_wall_timing_definition" ||
                            item.GroupName == "scenario_pda")
                            continue;

                        var tagname = $"{item.Name}.{item.GroupName}";

                        if (list.Contains(tagname))
                            continue;

                        list.Add(tagname);

                        if (item.Name.Contains("."))
                            continue;

                        if (!CacheContext.TryGetTag(tagname, out var edInstance))
                            continue;

                        if (edInstance == null)
                            continue;

                        var blamContext = new CacheSerializationContext(ref BlamCache, item);
                        var bmTag = blamDeserializer.Deserialize<SoundLooping>(blamContext);

                        using (var cacheStream = CacheContext.OpenTagCacheRead())
                        {
                            var tagContext = new TagSerializationContext(cacheStream, CacheContext, edInstance);
                            var edTag = CacheContext.Deserialize<SoundLooping>(tagContext);

                            if (edTag.Flags != bmTag.Flags) goto Continue;
                            if (edTag.MartySMusicTime != bmTag.MartySMusicTime) goto Continue;
                            if (edTag.Unknown1 != bmTag.Unknown1) goto Continue;
                            if (edTag.Unknown2 != bmTag.Unknown2) goto Continue;
                            if (edTag.Unknown3 != bmTag.Unknown3) goto Continue;
                            if (edTag.Unused != bmTag.Unused) goto Continue;
                            // if (edTag.SoundClass != bmTag.SoundClass) goto Continue;
                            if (edTag.Unknown4 != bmTag.Unknown4) goto Continue;
                            if (edTag.Tracks.Count != bmTag.Tracks.Count) goto Continue;
                            if (edTag.DetailSounds.Count != bmTag.DetailSounds.Count) goto Continue;

                            for (int i = 0; i < edTag.Tracks.Count; i++)
                            {
                                var edTrack = edTag.Tracks[i];
                                var bmTrack = bmTag.Tracks[i];

                                if (CacheContext.StringIdCache.GetString(edTrack.Name) != BlamCache.Strings.GetItemByID(bmTrack.Name.Index))
                                    goto Continue;

                                IsDiffOrNull(edTrack.In, bmTrack.In, BlamCache);
                                IsDiffOrNull(edTrack.Loop, bmTrack.Loop, BlamCache);
                                IsDiffOrNull(edTrack.Out, bmTrack.Out, BlamCache);
                                IsDiffOrNull(edTrack.AlternateLoop, bmTrack.AlternateLoop, BlamCache);
                                IsDiffOrNull(edTrack.AlternateOut, bmTrack.AlternateOut, BlamCache);
                                IsDiffOrNull(edTrack.AlternateTransitionIn, bmTrack.AlternateTransitionIn, BlamCache);
                                IsDiffOrNull(edTrack.AlternateTransitionOut, bmTrack.AlternateTransitionOut, BlamCache);
                            }

                            Continue:
                            continue;

                        }
                    }
                }
            }

            CsvDumpQueueToFile(csvQueue1, $"{GetCurrentMethod()}.csv");
            CsvDumpQueueToFile(csvQueue2, $"{GetCurrentMethod()}_2.csv");

            return true;
        }

    }
}