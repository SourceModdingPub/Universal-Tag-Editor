﻿using TagTool.Cache;
using TagTool.Commands;
using TagTool.Geometry;
using TagTool.Serialization;
using TagTool.Shaders;
using TagTool.Tags.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using TagTool.Common;
using TagTool.ShaderGenerator;
using TagTool.ShaderGenerator.Types;
using System.Linq;
using static TagTool.Tags.Definitions.RenderMethodTemplate.DrawModeRegisterOffsetBlock;

namespace TagTool.Commands.Shaders
{
    class GenerateRenderMethodTemplate : Command
    {
        private GameCacheContext CacheContext { get; }
        private CachedTagInstance Tag { get; }
        private RenderMethodTemplate Definition { get; }

        public GenerateRenderMethodTemplate(GameCacheContext cacheContext, CachedTagInstance tag, RenderMethodTemplate definition) :
            base(CommandFlags.Inherit,

                "Generate",
                "Compiles HLSL source file from scratch :D",
                "Generate <index> <shader_type> <drawmode> <parameters...>",
                "Compiles HLSL source file from scratch :D")
        {
            CacheContext = cacheContext;
            Tag = tag;
            Definition = definition;
        }

        class Mapping
        {
            public readonly ShaderParameter.RType ExpectedType;
            public string Name;

            public Mapping(string name, ShaderParameter.RType expectedtype, DrawModeRegisterOffsetTypeBits supported_registers)
            {
                Name = name;
                ExpectedType = expectedtype;
            }
        }

        static Mapping[] MappingsSource = new Mapping[]
        {
            
        };

        static Dictionary<string, Mapping> MappingsLookup = SetupMappings();

        private static Dictionary<string, Mapping> SetupMappings()
        {
            Dictionary<string, Mapping> dictionary = new Dictionary<string, Mapping>();

            foreach(var mapping in MappingsSource)
            {
                if (dictionary.ContainsKey(mapping.Name)) throw new Exception("Duplicate Mapping! Bad!");
                dictionary[mapping.Name] = mapping;
            }

            return dictionary;
        }

        public override object Execute(List<string> args)
        {
            if (args.Count <= 0)
                return false;

            if(args.Count < 2)
            {
                Console.WriteLine("Invalid number of args");
                return false;
            }

            Int32 index;
            string type;
            try
            {
                index = Int32.Parse(args[0]);
                type = args[1].ToLower();
            } catch
            {
                Console.WriteLine("Invalid index, type, and drawmode combination");
                return false;
            }

            Int32[] shader_args;
			try { shader_args = Array.ConvertAll(args.Skip(2).ToArray(), Int32.Parse); }
			catch { Console.WriteLine("Invalid shader arguments! (could not parse to Int32[].)"); return false; }

			// runs the appropriate shader-generator for the template type.
            switch(type)
            {
                case "beam_templates":
                case "beam_template":
                    {
                        var result_default = new BeamTemplateShaderGenerator(CacheContext, TemplateShaderGenerator.Drawmode.Default, shader_args)?.Generate();

                        //TODO: Figure out the rest of RMT2 rip

                        Definition.DrawModeBitmask = 0;
                        Definition.DrawModeBitmask |= RenderMethodTemplate.ShaderModeBitmask.Default;

                        //TODO: Replace Vertex and Pixl Shaders
                        //VertexShader;
                        //PixelShader

                        Definition.DrawModes = new List<RenderMethodTemplate.DrawMode>();
                        Definition.ArgumentMappings = new List<RenderMethodTemplate.ArgumentMapping>();
                        Definition.DrawModeRegisterOffsets = new List<RenderMethodTemplate.DrawModeRegisterOffsetBlock>();

                        Definition.Arguments = new List<RenderMethodTemplate.ShaderArgument>();
                        Definition.Unknown5 = new List<RenderMethodTemplate.ShaderArgument>();
                        Definition.GlobalArguments = new List<RenderMethodTemplate.ShaderArgument>();
                        Definition.ShaderMaps = new List<RenderMethodTemplate.ShaderArgument>();
                        
                        


        //                        public List<DrawMode> DrawModes; // Entries in here correspond to an enum in the EXE
        //                        public List<UnknownBlock2> Unknown3;
        //                        public List<ArgumentMapping> ArgumentMappings;
        //                        public List<Argument> Arguments;
        //                        public List<UnknownBlock4> Unknown5;
        //                        public List<UnknownBlock5> Unknown6;
        //                        public List<ShaderMap> ShaderMaps;






        //Definition.ShaderMaps = new List<RenderMethodTemplate.ShaderMap>();



                    }
					break;
				case "contrail_templates":
                case "contrail_template":
				case "cortana_templates":
				case "cortana_template":
				case "custom_templates":
				case "custom_template":
				case "decal_templates":
                case "decal_template":
                case "foliage_templates":
                case "foliage_template":
                case "halogram_templates":
                case "halogram_template":
                case "light_volume_templates":
                case "light_volume_template":
				case "particle_templates":
				case "particle_template":
				case "screen_templates":
				case "screen_template":
				case "shader_templates":
                case "shader_template":
                case "terrain_templates":
                case "terrain_template":
                case "water_templates":
                case "water_template":
                    Console.WriteLine($"{type} is not implemented");
                    return false;
                default:
                    Console.WriteLine($"Unknown template {type}");
                    return false;
            }

            return true;
        }

        public List<ShaderParameter> GetParamInfo(string assembly)
        {
            var parameters = new List<ShaderParameter> { };

            using (StringReader reader = new StringReader(assembly))
            {
                if (string.IsNullOrEmpty(assembly))
                    return null;

                string line;

                while (!(line = reader.ReadLine()).Contains("//   -"))
                    continue;

                while (!string.IsNullOrEmpty((line = reader.ReadLine())))
                {
                    line = (line.Replace("//   ", "").Replace("//", "").Replace(";", ""));

                    while (line.Contains("  "))
                        line = line.Replace("  ", " ");

                    if (!string.IsNullOrEmpty(line))
                    {
                        var split = line.Split(' ');
                        parameters.Add(new ShaderParameter
                        {
                            ParameterName = CacheContext.GetStringId(split[0]),
                            RegisterType = (ShaderParameter.RType)Enum.Parse(typeof(ShaderParameter.RType), split[1][0].ToString()),
                            RegisterIndex = byte.Parse(split[1].Substring(1)),
                            RegisterCount = byte.Parse(split[2])
                        });
                    }
                }
            }

            return parameters;
        }
    }
}