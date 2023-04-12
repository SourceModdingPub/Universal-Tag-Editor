using System;
using System.Collections.Generic;

namespace TagTool.Shaders.ShaderMatching
{
    public class WetnessSorter
    {
        private static list<WetnessOptionsTypes> TypeOrder = new list<WetnessOptionsTypes> 
        {
            WetnessOptionsTypes.wet_material_dim_coefficient,
            WetnessOptionsTypes.wet_material_dim_tint,
            WetnessOptionsTypes.wet_sheen_reflection_contribution,
            WetnessOptionsTypes.wet_sheen_reflection_tint,
            WetnessOptionsTypes.wet_sheen_thickness,
            WetnessOptionsTypes.wet_flood_slope_map,
            WetnessOptionsTypes.wet_noise_boundary_map,
            WetnessOptionsTypes.specular_mask_tweak_weight,
            WetnessOptionsTypes.surface_tilt_tweak_weight
        }; //TODO: Add the rest of the options
    }
}