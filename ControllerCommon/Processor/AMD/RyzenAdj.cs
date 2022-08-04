﻿using System;
using System.Runtime.InteropServices;

namespace ControllerCommon.Processor.AMD
{
    public enum RyzenFamily
    {
        FAM_UNKNOWN = -1,
        FAM_RAVEN = 0,
        FAM_PICASSO,
        FAM_RENOIR,
        FAM_CEZANNE,
        FAM_DALI,
        FAM_LUCIENNE,
        FAM_VANGOGH,
        FAM_REMBRANDT,
        FAM_END
    };

    public class RyzenAdj
    {
        [DllImport("libryzenadj.dll")] public static extern IntPtr init_ryzenadj();
        [DllImport("libryzenadj.dll")] public static extern int set_stapm_limit(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_fast_limit(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_slow_limit(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_slow_time(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_stapm_time(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_tctl_temp(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_vrm_current(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_vrmsoc_current(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_vrmmax_current(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_vrmsocmax_current(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_psi0_current(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_psi0soc_current(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_max_gfxclk_freq(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_min_gfxclk_freq(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_max_socclk_freq(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_min_socclk_freq(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_max_fclk_freq(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_min_fclk_freq(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_max_vcn(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_min_vcn(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_max_lclk(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_min_lclk(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_gfx_clk(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_oc_clk(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_per_core_oc_clk(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_oc_volt(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int disable_oc(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern int enable_oc(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern int set_prochot_deassertion_ramp(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_apu_skin_temp_limit(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_dgpu_skin_temp_limit(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_apu_slow_limit(IntPtr ry, [In] uint value);
        [DllImport("libryzenadj.dll")] public static extern int set_power_saving(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern int set_max_performance(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern int refresh_table(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern IntPtr get_table_values(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_stapm_limit(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_stapm_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_stapm_time(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_fast_limit(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_fast_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_slow_limit(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_slow_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_apu_slow_limit(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_apu_slow_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrm_current(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrm_current_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrmsoc_current(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrmsoc_current_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrmmax_current(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrmmax_current_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrmsocmax_current(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_vrmsocmax_current_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_tctl_temp(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_tctl_temp_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_apu_skin_temp_limit(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_apu_skin_temp_value(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_dgpu_skin_temp_limit(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_dgpu_skin_temp_value(IntPtr ry);

        [DllImport("libryzenadj.dll")] public static extern float get_core_clk(IntPtr ry, uint value);
        [DllImport("libryzenadj.dll")] public static extern float get_core_temp(IntPtr ry, uint value);
        [DllImport("libryzenadj.dll")] public static extern float get_core_power(IntPtr ry, uint value);
        [DllImport("libryzenadj.dll")] public static extern float get_core_volt(IntPtr ry, uint value);

        [DllImport("libryzenadj.dll")] public static extern float get_l3_logic(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_l3_vddm(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_l3_temp(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_l3_clk(IntPtr ry);

        [DllImport("libryzenadj.dll")] public static extern float get_gfx_clk(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_gfx_temp(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_gfx_volt(IntPtr ry);

        [DllImport("libryzenadj.dll")] public static extern float get_mem_clk(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_fclk(IntPtr ry);

        [DllImport("libryzenadj.dll")] public static extern float get_soc_power(IntPtr ry);
        [DllImport("libryzenadj.dll")] public static extern float get_soc_volt(IntPtr ry);

        [DllImport("libryzenadj.dll")] public static extern float get_socket_power(IntPtr ry);

        [DllImport("libryzenadj.dll")] public static extern RyzenFamily get_cpu_family(IntPtr ry);
    }
}
