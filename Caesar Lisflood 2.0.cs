
// CAESAR-Lisflood hydrodynamic and morphodynamic Landscape Evolution model Copyright (C) 2023 Tom.J.Coulthard 
//This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. 
//This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; 
//without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details. 
//You should have received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.


// 1.8d notes. tab/space/comma separated input files
// fixed error relating to jmean and min time step.
// 5960 - added baseflow instead of j_mean in get_catchment_input_points() make it similar to 1.7a
// 6001 - edited numberof points counter - was possibly overwriting by one...

// 1.8f notes - now speeds up if running with less than 9 grainsizes, vectors in outputs returns..

// Added by Matt Wilson to 1.8f:
// 12/03/16: Spatially distributed friction
// 13/03/16: Water source tracing
// 01/04/16: Rainfall zone water source tracing
// 18/04/16: PNG world file (pngw) created as part of Google animations, allowing easy
// loading of graphics into GIS

// Summary of additions by Matt Wilson to v2.0 (June 2023) - code changes/ additions tagged with MDW_V2:
// 1. Extra sources file added to hydrology tab: csv file with 3 columns provided (format: x,y,sourcefile.csv).
//    This allows unlimited extra sources to be included and traced. 
// 2. Improved the selection of water sources in the visualisation (properly labelled now). Various bug fixes as well.
// 3. Water solute tracer functionality added: solute concentrations (units not important) can be added to
//    the reach input files (columns 15 +). A simple depth averaging is then used for routing the solutes downstream.
// 4. The file load code for hydro inputs has been made more robust, e.g., comments can now be included in reach
//    input files, overruns avoided.
// 5. Various bits of code tidying, neatening the hydrology tab layout.
// 6. Added a dialog to change the output folder to the files tab. A checkbox to create a subfolder with the start
//    date/time of the simulation has also been added - so each simulation will now create its own folder if this
//    is checked.
// 7. Added all sources/solutes to the point info window, if tracing for each is active. 

// Main changes for v2.0.1 (tagged with MDW_Apr24):
// 1. Fixed tracer issues for drying cells, causing tracersum > 1 
// 2. Fixed solute tracer issue leading to very large solute values due to very low depths, in update_tracer_states()
// 3. Disabled scaling of solutes in evaporate(), as it was leading to very large concentrations as depth goes to zero. Needs a rethink.
// 4. Fixed stack overflow issue when timeseries input files are longer than required
// 5. Added debug info and stack trace to error messages (which are mostly due to input file errors)

// Oil spill simulation functionality added by Paola Di Fluri and Matt Wilson 2025 (tagged with OIL_V1)
// Dynamic temperature additions added July 2026 by Matt Wilson and Paola Di Fluri (tagged with TEMP_V1)


using System;
using System.Collections;
using System.Collections.Generic; // MDW
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq; //MDW
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Security.Policy;
using System.Threading;//PDF
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;  //JMW
using System.Xml.Linq;
using System.Xml.Schema;

namespace caesar1
{
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    /// 



    public class Form1 : System.Windows.Forms.Form
    {


        private System.Drawing.Bitmap m_objDrawingSurface;
        //JMW
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern long BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth,
            int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwROP);

        private System.ComponentModel.IContainer components;

        //Jorge variables

        public static double magnifyValue = 0;
        public static int updateClick = 0;
        private double[] zoomFactor = { .25, .33, .50, .66, .80, 1, 1.25, 1.5, 2.0, 2.5, 3.0 };
        private double[] contrastFactor = { 1, 1.2, 1.4, 1.6, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8, 3 };
        private double contrastMultiplier = 0;
        public int imageCount = 1;
        public int imageCount2 = 0;
        int coordinateDone = 0;
        double urfinalLati, urfinalLongi, llfinalLati, llfinalLongi, yurcorner, xurcorner = 0;
        public string kml = "";
        public string KML_FILE_NAME = "animation\\animation.kml";
        int save_time2, save_interval2 = 0;
        public string startDate, kmlTime;
        public DateTime googleTime;
        public string[] DateArray;
        public string[] DateArray2;


        // toms global variables
        double gravity = 9.81;
        double water_depth_erosion_threshold = 0.01;
        int input_time_step = 60;
        int number_of_points = 0;
        double globalsediq = 0;
        double time_1 = 1;
        double save_time = 0;
        double creep_time = 1;
        double creep_time2 = 1;
        double soil_erosion_time = 1;
        double soil_development_time = 1;

        double bedrock_erosion_threshold = 0;
        double bedrock_erosion_rate = 0;

        int tracers = 0; // for(int T=0;T<=tracers;T++)

        int input_type_flag = 0; // 0 is water input from points, 1 is input from hydrograph or rainfall file.
        double failureangle = 45;
        double saveinterval = 1000;
        int counter = 0;
        System.Windows.Forms.Timer gameClock;
        bool googleoutputflag = false;
        double waterinput = 0;
        double waterOut = 0;
        double in_out_difference = 0;
        double mannings = 0.04;
        int rfnum = 1;

        int xmax, ymax;
        double xll, yll;

        int maxcycle;
        double ERODEFACTOR = 0.05;
        double DX = 5;
        double root = 7.07;

        int LIMIT = 1;
        double MIN_Q = 0.01;
        double CREEP_RATE = 0.0025;
        double SOIL_RATE = 0.0025;
        double active = 0.2;
        int G_MAX = 10;
        double lateral_constant = 0.0000002;
        int grain_array_tot = 1;

        double time_factor = 1;
        double[] j, jo, j_mean, old_j_mean, new_j_mean, dprop, dprop_, M;
        //double M=0.005;
        double baseflow = 0.00000005; //end of hyd model variables usually 0.0000005 changed 2/11/05
        public static double cycle = 0;
        double rain_factor = 1;
        double sediQ = 0;
        double grow_grass_time = 0;
        double duneupdatetime = 0;

        double output_file_save_interval = 60;
        double min_time_step = 0;
        double vegTauCrit = 100;
        public static int graphics_scale = 2; // value that controls the number of bmp pixels per model pixel for the output images.
        int max_time_step = 0;
        int dune_mult = 5;
        double dune_time = 1;
        double max_vel = 5;
        double sand_out = 0;
        double maxdepth = 10;
        double courant_number = 0.7;
        double erode_call = 0;
        double erode_mult = 1;
        double lateralcounter = 1;
        double edgeslope = 0.001;
        double bed_proportion = 0.01;
        double veg_lat_restriction = 0.1;
        double lateral_cross_channel_smoothing = 0.0001;
        double froude_limit = 0.8;
        double recirculate_proportion = 1;

        double Csuspmax = 0.05; // max concentration  of SS allowed in a cell (proportion)
        double hflow_threshold = 0.001;

        // KAtharine
        int variable_m_value_flag = 0;

        // grain size variables - the sizes
        double d1 = 0.0005;
        double d2 = 0.001;
        double d3 = 0.002;
        double d4 = 0.004;
        double d5 = 0.008;
        double d6 = 0.016;
        double d7 = 0.032;
        double d8 = 0.064;
        double d9 = 0.128;

        // grain size variables - the sizes for particle distribution2
        double d1_ = 0.0005;
        double d2_ = 0.001;
        double d3_ = 0.002;
        double d4_ = 0.004;
        double d5_ = 0.008;
        double d6_ = 0.016;
        double d7_ = 0.032;
        double d8_ = 0.064;
        double d9_ = 0.128;


        // Gez
        double previous;
        int hours = 0;
        double new_cycle = 0;
        double old_cycle = 0;
        double tx = 60;
        double Tx = 0;
        double tlastcalc = 0;
        double Qs_step = 0;
        double Qs_hour = 0;
        double Qs_over = 0;
        double Qs_last = 0;
        double Qw_newvol = 0;
        double Qw_oldvol = 0;
        double Qw_lastvol = 0;
        double Qw_stepvol = 0;
        double Qw_hourvol = 0;
        double Qw_hour = 0;
        double Qw_overvol = 0;
        double temptotal = 0;
        double old_sediq = 0;
        double[,] sum_grain, sum_grain2, old_sum_grain, old_sum_grain2, Qg_step, Qg_step2, Qg_hour, Qg_hour2, Qg_over, Qg_over2, Qg_last, Qg_last2;
        string CATCH_FILE = "catchment.dat";
        // end gez

        // toms global arrays
        public static double[,] elev, bedrock, init_elevs, water_depth, area, tempcreep, Tau, Vel, qx, qy,
            qx_prev, qy_prev, // OIL_V1 so that we can save the previous timestep of water routing
            /* dune arrays */ area_depth, sand, elev2, sand2, elev_diff, spat_var_mannings, erodetot, erodetot3, temp_elev;
        int[,] index, cross_scan, down_scan, rfarea, tracer_area, angle_threshold, grain_area;
        bool[,] inputpointsarray;
        int[] catchment_input_x_coord, catchment_input_y_coord;

        double[,,] vel_dir, grain, qxs, qys;
        double[,,,] strata;
        double[] temp_grain;
        double[,] hourly_rain_data, hourly_m_value, mine_inputs;
        double[,,] inputfile;
        int[,] inpoints;
        public static double[,,] veg;
        public static double[,] edge, edge2; //TJC 27/1/05 array for edges
        double[] old_j_mean_store;
        double[,] climate_data;
        double[,,,] sr, sl, su, sd;
        double[,,] ss;


        // MJ global vars
        double[] fallVelocity;
        bool[] isSuspended;
        double[,,] Vsusptot;
        int[] deltaX = new int[9] { 0, 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] deltaY = new int[9] { 0, -1, -1, 0, 1, 1, 1, 0, -1 };
        int[] nActualGridCells;
        double Jw_newvol = 0.0;
        double Jw_oldvol = 0.0;
        double Jw_lastvol = 0.0;
        double Jw_stepvol = 0.0;
        double Jw_hourvol = 0.0;
        double Jw_hour = 0.0;
        double Jw_overvol = 0.0;
        double k_evap = 0.0;

        // JOE global vars
        string[] inputheader;           //Read from ASCII DEM <JOE 20050605>
        double[,] slopeAnalysis;  // Initially calculated in percent slope, coverted to radians
        double[,] aspect;         // Radians
        double[,] hillshade;      // 0 to 255
        double hue = 360.0;     // Ranges between 0 and 360 degrees
        double sat = 0.90;      // Ranges between 0 and 1.0 (where 1 is 100%)
        double val = 1.0;       // Ranges between 0 and 1.0 (where 1 is 100%)
        double red = 0.0;
        double green = 0.0;
        double blue = 0.0;

        // siberia submodel parameters
        double m1 = 1.70;
        double n1 = 0.69;
        double Beta3 = 0.000186;
        double m3 = 0.79;
        double Beta1 = 1067;

        // sedi tpt flags
        int einstein = 0;
        int wilcock = 0;
        int meyer = 0;
        int div_inputs = 1;
        double rain_data_time_step = 60; // time step for rain data - default is 60.
        double mfiletimestep = 1440; // tiem step for variable M value file


        // lisflood caesar adaptation globals
        int[] catchment_input_counter;
        int totalinputpoints = 0;

        //JMW Vars
        string basetext = "CAESAR - Lisflood 2.0h (17/9/2024)";
        string cfgname = null;  //Config file name
        string workdir = "c:\\program files\\Caesar\\work\\";

        // folder for outputs - MDW_V2
        private string outdir = "";
        private bool outdirDateTime = false;
        private string googleAnimationDir = "animation"; // MDW_V2 - added to handle user folder


        // stage/tidal variables
        int fromx, tox, fromy, toy;
        double stage_input_time_step = 1;
        double[] stage_inputfile;

        // Soil generation variables
        double P1, b1, k1, c1, c2, k2, c3, c4;

        // MDW global vars
        public static double[,] spatialmannings, water_depth_prev, dhdt_x, dhdt_y; //spatial mannings, water state prior to routing
        public static double[,,] watertracer, watertracer_prev, watertracerRainZone, watertracerRainZone_prev; // stacked arrays for tracing water sources
        public static double[,,] solutetracer, solutetracer_prev; // stacked arrays for tracing time dynamic solutes in water sources - MDW_V2
        public static int[,] rainzonation; // rainfall zonation for routing
        public static int nSources = 0, nRainZones = 0; // number of water sources for tracing, number of rain zones
        public static int nSolutes = 0; // number of solutes to trace - MDW_V2
        public static bool isTraceWater = false; // Water source tracing (default to off) // MDW_V2 changed protection level
        public static bool isTraceRainZonation = false; // Water source tracing: rainfall zonation // MDW_V2 changed protection level
        public static bool isTraceSolutes = false; // Water source tracing: time varying solutes in input sources - MDW_V2
        public int[] sourceIDs = new int[0]; // to identify different sources where they are split over several cells // MDW_V2 modified to be initialised empty (not null)
        public static int[] rainZones = new int[0]; // list of rainfall zones
        public double[] soluteMax; // maximum of input solute concentrations, for normalisation during visualisation - MDW_V2
        public int[] trace_rgb, tracerain_rgb, tracesolute_rgb; // to save selected layer for water source visualisation // MDW_V2: added solute
        public bool tracergb_initialising = false; // to allow initialisation when switching modes
        public static double enhanceValue = 0.2; // for enhancement of low water concentration in visualisation
        private double[] enhanceFactor = { 1.0, .7, .6, .5, .4, .3, .25, .2, .17, .15, .13 };
        private string[] extraSourceFiles; // MDW_V2
        public static List<string> inputfilenames = new List<string>(); //MDW_V2 (moved to global from load_data)
        int nHydroChecked = 0; //MDW_V2 - needed to correctly index extra sources
        public static int sourceIndexAddition = 2; //MDW_V2 - source numbering adjustment to accrount for whether if rain/ tide is on

        public static bool simLoadState = false;

        // OIL_V1
        public int oil_fromx, oil_tox, oil_fromy, oil_toy, oil_n_cells, count_cells;
        public static bool isOilSimulation, oil_eventtriggered = false;
        public double oil_startt, oil_starth, oil_spillt, ro_water, oil_density, oil_T, cinematic_v, oil_ratev, oil_totalv, Cf,
            oil_Tg, oil_To, oil_B, oil_A, oil_transf_coeff, vel_wind, API, totalOilVolume, oil_out_t, oil_out_sum, oil_evap_t,
            oil_evap_sum, oil_in_sum, error_negative, error_negative_sum, error_edge, error_edge_sum,
            oil_S, oil_D, iteration_t, Ev, oil_exp, oil_depth_init, Ev_prev, Vout_total, Vout_1, Vout_2, Vout_3, Vout_4;
        public static double[,] oil_hx, oil_hy, oil_hx_prev, oil_hy_prev, oil_depth, oil_depth_prev, oil_evap_fraction, oil_conc, oil_concentration;

        // TEMP_V1 - water temperature module (see water temperature module spec doc)
        public static double[,] water_temp, water_temp_prev;
        public static int[,] met_zonation;
        public static int nMetZones = 1; 
        public static int[] metZones = new int[0];
        public static double[,] hourly_air_temp, hourly_shortwave, hourly_windspeed,
            hourly_humidity, hourly_cloudcover, hourly_pressure, hourly_dewpoint;
        public static bool isSimulateTemperature = false;
        public static bool useSimplifiedTempScheme = false;
        public static bool useHecRasLongwave = true;
        public static bool useHecRasAlbedo = true;
        public double met_data_time_step = 60;
        public double thermal_update_interval = 60;
        public double thermal_time = 0;
        public double siteLatitude = 0, siteLongitude = 0, siteTimeZone = 0,
            siteElevation = 0, windMeasurementHeight = 10;
        public double diffusivityRatio = 1.0;
        public double albedoWaterBase = 0.08, albedoSedimentCoeff = 0, suspCondRef = 1;
        public double windFunc_a = 1e-6, windFunc_b = 1e-6, windFunc_c = 1;
        public double waterTempInitialValue = 15.0; // TEMP_V1 - constant initial water temperature, GUI-set

        // TC mining
        int minesitenumber = 0;

        private Graphics mygraphics;
        //private Boolean DoingGraphics;  // <JMW 20041108>
        //Form2 form2; // <JMW 20041018>
        // JMW end

        #region windows_forms_and_controls
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.MainMenu mainMenu1;
        private System.Windows.Forms.MenuItem menuItem1;
        private System.Windows.Forms.MenuItem menuItem3;
        private System.Windows.Forms.MenuItem menuItem4;
        private System.Windows.Forms.MenuItem menuItem5;
        private System.Windows.Forms.MenuItem menuItem11;
        private System.Windows.Forms.MenuItem menuItem12;
        private System.Windows.Forms.MenuItem menuItem13;
        private System.Windows.Forms.MenuItem menuItem14;
        private System.Windows.Forms.MenuItem menuItem25;
        private System.Windows.Forms.MenuItem menuItemConfigFile;
        private System.Windows.Forms.MenuItem menuItemConfigFileOpen;
        private System.Windows.Forms.MenuItem menuItemConfigFileSave;
        private System.Windows.Forms.MenuItem menuItemConfigFileSaveAs;
        private System.Windows.Forms.MenuItem menuItem2;
        private System.Windows.Forms.MenuItem menuItem9;
        private System.Windows.Forms.MenuItem menuItem8;
        private System.Windows.Forms.MenuItem menuItem7;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage GridTab;
        private System.Windows.Forms.TabPage NumericalTab;
        private System.Windows.Forms.TabPage GrainTab;
        private System.Windows.Forms.TabPage HydrologyTab;
        private System.Windows.Forms.TabPage OilTab; // OIL_V1
        private System.Windows.Forms.TextBox gp3box;
        private System.Windows.Forms.TextBox gp4box;
        private System.Windows.Forms.TextBox gp5box;
        private System.Windows.Forms.TextBox gp6box;
        private System.Windows.Forms.TextBox gp7box;
        private System.Windows.Forms.TextBox gp8box;
        private System.Windows.Forms.TextBox gp9box;
        private System.Windows.Forms.TextBox gp2box;
        private System.Windows.Forms.TextBox gp1box;
        private System.Windows.Forms.TextBox g3box;
        private System.Windows.Forms.TextBox g4box;
        private System.Windows.Forms.TextBox g5box;
        private System.Windows.Forms.TextBox g6box;
        private System.Windows.Forms.TextBox g7box;
        private System.Windows.Forms.TextBox g8box;
        private System.Windows.Forms.TextBox g9box;
        private System.Windows.Forms.TextBox g2box;
        private System.Windows.Forms.TextBox g1box;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label labelOutDir; // MDW_V2
        private System.Windows.Forms.TextBox textBoxOutDir; // MDW_V2
        private System.Windows.Forms.FolderBrowserDialog folderBrowserOutDir; // MDW_V2
        private System.Windows.Forms.Button buttonOutDir; // MDW_V2
        private System.Windows.Forms.CheckBox checkboxOutDirDateTime; // MDW_V2
        private System.Windows.Forms.TextBox input_time_step_box;
        private System.Windows.Forms.TextBox infile4;
        private System.Windows.Forms.TextBox infile3;
        private System.Windows.Forms.TextBox infile2;
        private System.Windows.Forms.TextBox infile1;
        private System.Windows.Forms.TextBox ybox1;
        private System.Windows.Forms.TextBox ybox2;
        private System.Windows.Forms.TextBox ybox3;
        private System.Windows.Forms.TextBox ybox4;
        private System.Windows.Forms.TextBox xbox2;
        private System.Windows.Forms.TextBox xbox3;
        private System.Windows.Forms.TextBox xbox4;
        private System.Windows.Forms.TextBox xbox1;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.Label label44;
        private System.Windows.Forms.Label label43;
        private System.Windows.Forms.Label label41;
        private System.Windows.Forms.CheckBox inbox2;
        private System.Windows.Forms.CheckBox inbox3;
        private System.Windows.Forms.CheckBox inbox4;
        private System.Windows.Forms.CheckBox inbox1;
        private System.Windows.Forms.Label label42;
        private System.Windows.Forms.TabPage DescriptionTab;
        private System.Windows.Forms.TextBox DescBox;
        private System.Windows.Forms.TextBox ytextbox;
        private System.Windows.Forms.TextBox xtextbox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox dxbox;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label49;
        private System.Windows.Forms.TextBox mintimestepbox;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.TextBox smoothbox;
        private System.Windows.Forms.TextBox cyclemaxbox;
        private System.Windows.Forms.TextBox itermaxbox;
        private System.Windows.Forms.TextBox limitbox;
        private System.Windows.Forms.Label label31;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label label47;
        private System.Windows.Forms.StatusBar statusBar1;
        private System.Windows.Forms.StatusBarPanel IterationStatusPanel;
        private System.Windows.Forms.StatusBarPanel TimeStatusPanel;
        private System.Windows.Forms.StatusBarPanel QwStatusPanel;
        private System.Windows.Forms.StatusBarPanel QsStatusPanel;
        private System.Windows.Forms.StatusBarPanel InfoStatusPanel;
        private System.Windows.Forms.StatusBarPanel tempStatusPanel;
        private System.Windows.Forms.Button start_button;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.MenuItem menuItem26;
        private System.Windows.Forms.MenuItem menuItem27;
        private System.Windows.Forms.MenuItem menuItem28;
        private System.Windows.Forms.MenuItem menuItem29;
        private System.Windows.Forms.CheckBox suspGS1box;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.TextBox fallGS2box;
        private System.Windows.Forms.TextBox fallGS1box;
        private System.Windows.Forms.CheckBox suspGS2box;
        private System.Windows.Forms.CheckBox suspGS3box;
        private System.Windows.Forms.CheckBox suspGS4box;
        private System.Windows.Forms.CheckBox suspGS5box;
        private System.Windows.Forms.CheckBox suspGS6box;
        private System.Windows.Forms.CheckBox suspGS7box;
        private System.Windows.Forms.CheckBox suspGS8box;
        private System.Windows.Forms.CheckBox suspGS9box;
        private System.Windows.Forms.Label gpSumLabel;
        private System.Windows.Forms.Label gpSumLabel2;
        private System.Windows.Forms.TextBox fallGS3box;
        private System.Windows.Forms.TextBox fallGS4box;
        private System.Windows.Forms.TextBox fallGS5box;
        private System.Windows.Forms.TextBox fallGS6box;
        private System.Windows.Forms.TextBox fallGS7box;
        private System.Windows.Forms.TextBox fallGS8box;
        private System.Windows.Forms.TextBox fallGS9box;
        private System.Windows.Forms.MenuItem menuItem30;
        private System.Windows.Forms.MenuItem menuItem31;
        private System.Windows.Forms.MenuItem menuItem33;
        private System.Windows.Forms.MenuItem menuItem34;
        private System.Windows.Forms.CheckBox overrideheaderBox;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.CheckBox recirculatebox;
        private System.Windows.Forms.Panel Panel1;
        private System.Windows.Forms.Label label52;
        private System.Windows.Forms.CheckBox bedslope_box;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.TextBox tempdata1;
        private System.Windows.Forms.TextBox tempdata2;
        private System.Windows.Forms.CheckBox veltaubox;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TextBox vegTauCritBox;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.TextBox infile5;
        private System.Windows.Forms.TextBox ybox5;
        private System.Windows.Forms.TextBox xbox5;
        private System.Windows.Forms.CheckBox inbox5;
        private System.Windows.Forms.TextBox infile6;
        private System.Windows.Forms.TextBox ybox6;
        private System.Windows.Forms.TextBox xbox6;
        private System.Windows.Forms.CheckBox inbox6;
        private System.Windows.Forms.TextBox infile7;
        private System.Windows.Forms.TextBox ybox7;
        private System.Windows.Forms.TextBox xbox7;
        private System.Windows.Forms.CheckBox inbox7;
        private System.Windows.Forms.TextBox infile8;
        private System.Windows.Forms.TextBox ybox8;
        private System.Windows.Forms.TextBox xbox8;
        private System.Windows.Forms.CheckBox inbox8;
        private System.Windows.Forms.CheckBox inboxExtra; //MDW_V2
        private System.Windows.Forms.TextBox infileExtra; //MDW_V2
        private TabPage FilesTab;
        private CheckBox reach_mode_box;
        private CheckBox catchment_mode_box;
        private CheckBox checkBoxGenerateTimeSeries;
        private CheckBox checkBoxGenerateIterations;
        private TextBox IterationOutbox;
        private TextBox TimeseriesOutBox;
        private TextBox outputfilesaveintervalbox;
        private TextBox saveintervalbox;
        private Label label45;
        private Label label33;
        private CheckBox uniquefilecheck;
        private Label label32;
        private TextBox tracerhydrofile;
        private TextBox mine_input_textBox;
        private TextBox bedrockbox;
        private TextBox graindataloadbox;
        private TextBox openfiletextbox;
        private CheckBox tracerbox;
        private Label label30;
        private Label label39;
        private Label label24;
        private Label label23;
        private CheckBox flowonlybox;
        private Smallwisdom.Windows.Forms.ZoomPanImageBox zoomPanImageBox1;
        private TrackBar trackBar1;
        private Label label61;
        private GroupBox groupBox2;
        private ComboBox comboBox1;
        private Label label62;
        private GroupBox groupBox3;
        private Label label63;
        private TrackBar trackBar2;
        private TextBox tracerOutputtextBox;
        private CheckBox tracerOutcheckBox;
        private TextBox mvaluebox;
        private Label label37;
        private TextBox grasstextbox;
        private Label label40;
        private TabPage tabPage4;
        private Label label67;
        private TextBox soil_ratebox;
        private TextBox slopebox;
        private TextBox creepratebox;
        private Label label34;
        private Label label8;
        private Label label69;
        private Label label68;
        private Label label70;
        private Label label73;
        private Label label72;
        private Label label71;
        private TextBox m3Box;
        private TextBox n1Box;
        private TextBox m1Box;
        private TextBox Beta3Box;
        private TextBox Beta1Box;
        private CheckBox SiberiaBox;
        private Label label75;
        private Label label74;
        private TextBox max_time_step_Box;
        private Label label76;
        private TextBox googleBeginDate;
        private Label label78;
        private TextBox googAnimationSaveInterval;
        private Label label79;
        private TextBox googleAnimationTextBox;
        private CheckBox googleAnimationCheckbox;
        private Button graphicToGoogleEarthButton;
        private CheckBox einsteinbox;
        private CheckBox wilcockbox;
        private Label label83;
        private TextBox div_inputs_box;
        private CheckBox checkBox1;
        private TabPage tabPage5;
        private CheckBox DuneBox;
        private Label label89;
        private Label label88;
        private Label label87;
        private Label label86;
        private Label label85;
        private Label label84;
        private TextBox slab_depth_box;
        private TextBox shadow_angle_box;
        private TextBox upstream_check_box;
        private TextBox depo_prob_box;
        private TextBox offset_box;
        private TextBox init_depth_box;
        private CheckBox bedslopebox2;
        private Label label56;
        private TextBox dune_time_box;
        private Label label57;
        private TextBox dune_grid_size_box;
        private GroupBox groupBox4;
        private TextBox textBox6;
        private CheckBox UTMsouthcheck;
        private TextBox UTMzonebox;
        private CheckBox UTMgridcheckbox;
        private TextBox textBox5;
        private CheckBox soilerosionBox;
        private CheckBox landslidesBox;
        private TextBox fraction_dune;
        private Label label54;
        private TextBox propremaining;
        private Label label50;
        private TextBox activebox;
        private TextBox erodefactorbox;
        private Label label12;
        private Label label48;
        private BackgroundWorker backgroundWorker1;
        private TextBox lateralratebox;
        private TextBox textBox3;
        private Label label60;
        private TextBox avge_smoothbox;
        private CheckBox newlateral;
        private Label label7;
        private TabPage tabPage1;
        private TextBox courantbox;
        private Label label38;
        private Label label53;
        private TextBox Q2box;
        private Label label3;
        private TextBox k_evapBox;
        private TextBox textBox2;
        private Label label46;
        private TextBox minqbox;
        private TextBox initscansbox;
        private Label label9;
        private Label label5;
        private CheckBox nolateral;
        private Label label55;
        private TextBox max_vel_box;
        private TextBox veg_lat_box;
        private Label label51;
        private Label label58;
        private TextBox downstreamshiftbox;
        private TextBox textBox4;
        private Label label64;
        private Label label65;
        private TextBox textBox7;
        private TextBox textBox8;
        private TextBox textBox9;
        private Label label77;
        public static CheckBox checkBox2;
        private TextBox MinQmaxvalue;
        private CheckBox checkBox3;
        private Label label90;
        private TextBox TidalFileName;
        private TextBox TidalInputStep;
        private Label label82;
        private TextBox TidalYmin;
        private TextBox TidalYmax;
        private TextBox TidalXmax;
        private TextBox TidalXmin;
        private Label label80;
        private Label label81;
        private GroupBox groupBoxWaterSourceTracer; // MDW_V2
        private GroupBox groupBoxOutputDirectory; // MDW_V2
        private GroupBox groupBox5;
        private GroupBox groupBox1;
        private GroupBox groupBox6;
        private Label label91;
        private TextBox textBox10;
        private TextBox bedrock_erosion_threshold_box;
        private TextBox bedrock_erosion_rate_box;
        private Label label92;
        private Label label93;
        private TabPage tabPage3;
        private Label label101;
        private Label label100;
        private Label label99;
        private TextBox textBox18;
        private TextBox textBox17;
        private TextBox textBox16;
        private Label label98;
        private Label label97;
        private Label label96;
        private TextBox textBox15;
        private TextBox textBox14;
        private TextBox textBox13;
        private Label label95;
        private Label label94;
        private TextBox textBox12;
        private TextBox textBox11;
        private CheckBox checkBox6;
        private CheckBox checkBox5;
        private CheckBox checkBox4;
        private CheckBox soildevbox;
        private GroupBox groupBox7;
        private Label label35;
        private TextBox raintimestepbox;
        private CheckBox jmeaninputfilebox;
        private Label label59;
        private TextBox mvalueloadbox;
        private TextBox raindataloadbox;
        private Label label25;
        private TextBox hydroindexBox;
        private Label label103;
        private Label label102;
        private TextBox rfnumBox;
        private CheckBox checkBox7;
        private CheckBox checkBox8;
        private TextBox textBox19;
        private Label label104;
        private CheckBox SpatVarManningsCheckbox;
        private Label label105;
        private TextBox mfiletimestepbox;
        private CheckBox meyerbox;
        private RadioButton radioButton1;
        private GroupBox groupBox8;
        private RadioButton radioButton2;
        private GroupBox groupBox9;
        private TextBox tracer_num;
        private CheckBox checkBox_tracer;
        private TextBox tracer_file;
        private TextBox textBox21;
        private Label label108;
        private TextBox angle_thresholdbox;
        private TextBox g2_box;
        private TextBox g1_box;
        private Label label109;
        private TrackBar trackBar3;
        private CheckBox checkBox12;
        private CheckBox checkBoxSoluteVis; // MDW_V2
        private Label label107;
        private ComboBox comboBox4;
        private Label label106;
        private ComboBox comboBox3;
        private ComboBox comboBox2;
        private Label label120;
        private Label label119;
        private Label label110;
        private CheckBox checkBoxSoluteTracer; //MDW_V2
        //private Label labelSoluteNumber; //MDW_V2
        //private TextBox numBoxSoluteNumber; //MDW_V2
        private TextBox textBox20;
        private CheckBox checkBox11;
        private CheckBox checkBox10;
        private MenuItem menuItem10;
        private MenuItem menuItemOilVisualisation; // OIL_V1_MDW
        private MenuItem menuItemOilconcVisualisation; // OIL_V1_PDF
        private MenuItem menuItemWaterTempVisualisation; // TEMP_V1
        private MenuItem menuItem6;
        private MenuItem menuItem15;
        private MenuItem menuItemSoluteTracer; //MDW_V2
        private MenuItem menuItemOilSpill; //OIL_V1_PDF
        private MenuItem menuItem17; //OIL_V1_PDF
        private MenuItem menuItemOilconc; //OIL_V1_PDF
        private Label label111;
        private Label label112;
        private Label label113;
        private Label label114;
        private Label label115;
        private Label label116;
        private Label label117;
        private Label label118;
        private TextBox gp9_box;
        private TextBox gp8_box;
        private TextBox gp7_box;
        private TextBox gp6_box;
        private TextBox gp5_box;
        private TextBox gp4_box;
        private TextBox gp3_box;
        private TextBox gp2_box;
        private TextBox gp1_box;
        private TextBox g9_box;
        private TextBox g8_box;
        private TextBox g7_box;
        private TextBox g6_box;
        private TextBox g5_box;
        private TextBox g4_box;
        private TextBox g3_box;
        private TextBox grain_index_file;
        private Label label121;
        private CheckBox landslide_grainsize;
        private MenuItem menuItem16;
        private Label label124;

        private Label label122;
        private Label label123;
        private Label label125;
        private Label label66;
        private CheckBox OilTab_checkBox;
        private GroupBox OilTab_groupBox_controls; // OIL_V1
        private TextBox OilYmin;
        private TextBox OilYmax;
        private TextBox OilXmax;
        private TextBox OilXmin;
        private Label OilYlabel;
        private Label OilXlabel;
        private Label OilTextlabel;
        private TextBox OilTimeMin;
        private Label OilTimeMinlabel;
        private TextBox OilDepthStart;
        private Label OilDepthStartlabel;
        private TextBox OilVolume;
        private Label OilVolumelabel;
        private TextBox OilSpillDuration;
        private Label OilSpillDurationlabel;

        private TabPage TempTab; // TEMP_V1
        private CheckBox TempTab_checkBox; // TEMP_V1
        private Label TempTab_label_airtemp; // TEMP_V1
        private TextBox TempTab_textBox_airtemp; // TEMP_V1
        private Label TempTab_label_shortwave; // TEMP_V1
        private TextBox TempTab_textBox_shortwave; // TEMP_V1
        private Label TempTab_label_windspeed; // TEMP_V1
        private TextBox TempTab_textBox_windspeed; // TEMP_V1
        private Label TempTab_label_humidity; // TEMP_V1
        private TextBox TempTab_textBox_humidity; // TEMP_V1
        private Label TempTab_label_cloudcover; // TEMP_V1
        private TextBox TempTab_textBox_cloudcover; // TEMP_V1
        private Label TempTab_label_pressure; // TEMP_V1
        private TextBox TempTab_textBox_pressure; // TEMP_V1
        private Label TempTab_label_dewpoint; // TEMP_V1
        private TextBox TempTab_textBox_dewpoint; // TEMP_V1

        // TEMP_V1 - scheme selection
        private GroupBox TempTab_groupBox_scheme;
        private RadioButton TempTab_radio_fullscheme;
        private RadioButton TempTab_radio_simplifiedscheme;
        private CheckBox TempTab_checkBox_hecraslongwave;
        private CheckBox TempTab_checkBox_hecrasalbedo;
        private Label TempTab_label_thermalinterval;
        private TextBox TempTab_textBox_thermalinterval;
        private Label TempTab_label_mettimestep;
        private TextBox TempTab_textBox_mettimestep;

        // TEMP_V1 - site parameters
        private GroupBox TempTab_groupBox_site;
        private Label TempTab_label_latitude;
        private TextBox TempTab_textBox_latitude;
        private Label TempTab_label_longitude;
        private TextBox TempTab_textBox_longitude;
        private Label TempTab_label_timezone;
        private TextBox TempTab_textBox_timezone;
        private Label TempTab_label_elevation;
        private TextBox TempTab_textBox_elevation;
        private Label TempTab_label_windheight;
        private TextBox TempTab_textBox_windheight;

        // TEMP_V1 - initial condition
        private GroupBox TempTab_groupBox_initial;
        private Label TempTab_label_initialtemp;
        private TextBox TempTab_textBox_initialtemp;
        private Label TempTab_label_initialraster;
        private TextBox TempTab_textBox_initialraster;
        private Label TempTab_label_sourcetemp; // TEMP_V1
        private TextBox TempTab_textBox_sourcetemp; // TEMP_V1
        public static double[,] sourceTempData; // TEMP_V1 - [row, column], gap-filled at load time
        public static int[] sourceTempColumnFor; // TEMP_V1 - [sourceIndex] -> column in sourceTempData, or -1 = no column (use waterTempInitialValue)
        public static int nSourceTempColumns = 0;
        public static DateTime simulationStartDateTime = new DateTime(2000, 6, 21); // TEMP_V1 - anchors cycle (minutes) to a real calendar date/time for solar geometry
        private Label TempTab_label_startdate;
        private TextBox TempTab_textBox_startdate;
        #endregion


        public Form1()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.button2 = new System.Windows.Forms.Button();
            this.mainMenu1 = new System.Windows.Forms.MainMenu(this.components);
            this.menuItemConfigFile = new System.Windows.Forms.MenuItem();
            this.menuItemConfigFileOpen = new System.Windows.Forms.MenuItem();
            this.menuItemConfigFileSaveAs = new System.Windows.Forms.MenuItem();
            this.menuItemConfigFileSave = new System.Windows.Forms.MenuItem();
            this.menuItem1 = new System.Windows.Forms.MenuItem();
            this.menuItem30 = new System.Windows.Forms.MenuItem();
            this.menuItem3 = new System.Windows.Forms.MenuItem();
            this.menuItem4 = new System.Windows.Forms.MenuItem();
            this.menuItem5 = new System.Windows.Forms.MenuItem();
            this.menuItem10 = new System.Windows.Forms.MenuItem();
            this.menuItemOilVisualisation = new System.Windows.Forms.MenuItem(); // OIL_V1_MDW
            this.menuItemOilconcVisualisation = new System.Windows.Forms.MenuItem(); // OIL_V1_PDF
            this.menuItemWaterTempVisualisation = new System.Windows.Forms.MenuItem(); // TEMP_V1
            this.menuItem2 = new System.Windows.Forms.MenuItem();
            this.menuItem7 = new System.Windows.Forms.MenuItem();
            this.menuItem8 = new System.Windows.Forms.MenuItem();
            this.menuItem9 = new System.Windows.Forms.MenuItem();
            this.menuItem26 = new System.Windows.Forms.MenuItem();
            this.menuItem27 = new System.Windows.Forms.MenuItem();
            this.menuItem28 = new System.Windows.Forms.MenuItem();
            this.menuItem31 = new System.Windows.Forms.MenuItem();
            this.menuItem11 = new System.Windows.Forms.MenuItem();
            this.menuItem12 = new System.Windows.Forms.MenuItem();
            this.menuItem13 = new System.Windows.Forms.MenuItem();
            this.menuItem14 = new System.Windows.Forms.MenuItem();
            this.menuItem25 = new System.Windows.Forms.MenuItem();
            this.menuItem29 = new System.Windows.Forms.MenuItem();
            this.menuItem33 = new System.Windows.Forms.MenuItem();
            this.menuItem34 = new System.Windows.Forms.MenuItem();
            this.menuItem6 = new System.Windows.Forms.MenuItem();
            this.menuItem15 = new System.Windows.Forms.MenuItem();
            this.menuItem16 = new System.Windows.Forms.MenuItem();
            this.menuItemSoluteTracer = new System.Windows.Forms.MenuItem();
            this.menuItemOilSpill = new System.Windows.Forms.MenuItem(); //OIL_V1_PDF
            this.menuItem17 = new System.Windows.Forms.MenuItem(); //OIL_V1_PDF
            this.menuItemOilconc = new System.Windows.Forms.MenuItem(); //OIL_V1_PDF
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.FilesTab = new System.Windows.Forms.TabPage();
            this.label123 = new System.Windows.Forms.Label();
            this.label124 = new System.Windows.Forms.Label();
            this.label122 = new System.Windows.Forms.Label();
            this.grain_index_file = new System.Windows.Forms.TextBox();
            this.label121 = new System.Windows.Forms.Label();
            this.tracer_file = new System.Windows.Forms.TextBox();
            this.tracer_num = new System.Windows.Forms.TextBox();
            this.checkBox_tracer = new System.Windows.Forms.CheckBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            this.textBox21 = new System.Windows.Forms.TextBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.UTMsouthcheck = new System.Windows.Forms.CheckBox();
            this.UTMzonebox = new System.Windows.Forms.TextBox();
            this.UTMgridcheckbox = new System.Windows.Forms.CheckBox();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.googleBeginDate = new System.Windows.Forms.TextBox();
            this.label78 = new System.Windows.Forms.Label();
            this.googAnimationSaveInterval = new System.Windows.Forms.TextBox();
            this.label79 = new System.Windows.Forms.Label();
            this.googleAnimationTextBox = new System.Windows.Forms.TextBox();
            this.googleAnimationCheckbox = new System.Windows.Forms.CheckBox();
            this.checkBox3 = new System.Windows.Forms.CheckBox();
            this.outputfilesaveintervalbox = new System.Windows.Forms.TextBox();
            this.TimeseriesOutBox = new System.Windows.Forms.TextBox();
            this.tracerOutputtextBox = new System.Windows.Forms.TextBox();
            this.tracerOutcheckBox = new System.Windows.Forms.CheckBox();
            this.reach_mode_box = new System.Windows.Forms.CheckBox();
            this.catchment_mode_box = new System.Windows.Forms.CheckBox();
            this.checkBoxGenerateTimeSeries = new System.Windows.Forms.CheckBox();
            this.checkBoxGenerateIterations = new System.Windows.Forms.CheckBox();
            this.IterationOutbox = new System.Windows.Forms.TextBox();
            this.saveintervalbox = new System.Windows.Forms.TextBox();
            this.label45 = new System.Windows.Forms.Label();
            this.label33 = new System.Windows.Forms.Label();
            this.uniquefilecheck = new System.Windows.Forms.CheckBox();
            this.tracerhydrofile = new System.Windows.Forms.TextBox();
            this.mine_input_textBox = new System.Windows.Forms.TextBox();
            this.bedrockbox = new System.Windows.Forms.TextBox();
            this.graindataloadbox = new System.Windows.Forms.TextBox();
            this.openfiletextbox = new System.Windows.Forms.TextBox();
            this.tracerbox = new System.Windows.Forms.CheckBox();
            this.label39 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.groupBoxOutputDirectory = new System.Windows.Forms.GroupBox();
            this.labelOutDir = new System.Windows.Forms.Label();
            this.textBoxOutDir = new System.Windows.Forms.TextBox();
            this.buttonOutDir = new System.Windows.Forms.Button();
            this.checkboxOutDirDateTime = new System.Windows.Forms.CheckBox();
            this.NumericalTab = new System.Windows.Forms.TabPage();
            this.bedslopebox2 = new System.Windows.Forms.CheckBox();
            this.max_time_step_Box = new System.Windows.Forms.TextBox();
            this.label76 = new System.Windows.Forms.Label();
            this.veltaubox = new System.Windows.Forms.CheckBox();
            this.label52 = new System.Windows.Forms.Label();
            this.bedslope_box = new System.Windows.Forms.CheckBox();
            this.label47 = new System.Windows.Forms.Label();
            this.mintimestepbox = new System.Windows.Forms.TextBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.smoothbox = new System.Windows.Forms.TextBox();
            this.cyclemaxbox = new System.Windows.Forms.TextBox();
            this.itermaxbox = new System.Windows.Forms.TextBox();
            this.limitbox = new System.Windows.Forms.TextBox();
            this.label31 = new System.Windows.Forms.Label();
            this.label27 = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.label49 = new System.Windows.Forms.Label();
            this.GrainTab = new System.Windows.Forms.TabPage();
            this.label125 = new System.Windows.Forms.Label();
            this.landslide_grainsize = new System.Windows.Forms.CheckBox();
            this.label120 = new System.Windows.Forms.Label();
            this.label119 = new System.Windows.Forms.Label();
            this.label111 = new System.Windows.Forms.Label();
            this.label112 = new System.Windows.Forms.Label();
            this.label113 = new System.Windows.Forms.Label();
            this.label114 = new System.Windows.Forms.Label();
            this.label115 = new System.Windows.Forms.Label();
            this.label116 = new System.Windows.Forms.Label();
            this.label117 = new System.Windows.Forms.Label();
            this.label118 = new System.Windows.Forms.Label();
            this.gp9_box = new System.Windows.Forms.TextBox();
            this.gp8_box = new System.Windows.Forms.TextBox();
            this.gp7_box = new System.Windows.Forms.TextBox();
            this.gp6_box = new System.Windows.Forms.TextBox();
            this.gp5_box = new System.Windows.Forms.TextBox();
            this.gp4_box = new System.Windows.Forms.TextBox();
            this.gp3_box = new System.Windows.Forms.TextBox();
            this.gp2_box = new System.Windows.Forms.TextBox();
            this.gp1_box = new System.Windows.Forms.TextBox();
            this.g9_box = new System.Windows.Forms.TextBox();
            this.g8_box = new System.Windows.Forms.TextBox();
            this.g7_box = new System.Windows.Forms.TextBox();
            this.g6_box = new System.Windows.Forms.TextBox();
            this.g5_box = new System.Windows.Forms.TextBox();
            this.g4_box = new System.Windows.Forms.TextBox();
            this.g3_box = new System.Windows.Forms.TextBox();
            this.g2_box = new System.Windows.Forms.TextBox();
            this.g1_box = new System.Windows.Forms.TextBox();
            this.label32 = new System.Windows.Forms.Label();
            this.label30 = new System.Windows.Forms.Label();
            this.meyerbox = new System.Windows.Forms.CheckBox();
            this.checkBox8 = new System.Windows.Forms.CheckBox();
            this.bedrock_erosion_threshold_box = new System.Windows.Forms.TextBox();
            this.bedrock_erosion_rate_box = new System.Windows.Forms.TextBox();
            this.label92 = new System.Windows.Forms.Label();
            this.label93 = new System.Windows.Forms.Label();
            this.label65 = new System.Windows.Forms.Label();
            this.textBox7 = new System.Windows.Forms.TextBox();
            this.label58 = new System.Windows.Forms.Label();
            this.downstreamshiftbox = new System.Windows.Forms.TextBox();
            this.label55 = new System.Windows.Forms.Label();
            this.max_vel_box = new System.Windows.Forms.TextBox();
            this.textBox3 = new System.Windows.Forms.TextBox();
            this.label60 = new System.Windows.Forms.Label();
            this.avge_smoothbox = new System.Windows.Forms.TextBox();
            this.nolateral = new System.Windows.Forms.CheckBox();
            this.newlateral = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.lateralratebox = new System.Windows.Forms.TextBox();
            this.label48 = new System.Windows.Forms.Label();
            this.label54 = new System.Windows.Forms.Label();
            this.propremaining = new System.Windows.Forms.TextBox();
            this.label50 = new System.Windows.Forms.Label();
            this.activebox = new System.Windows.Forms.TextBox();
            this.erodefactorbox = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.einsteinbox = new System.Windows.Forms.CheckBox();
            this.wilcockbox = new System.Windows.Forms.CheckBox();
            this.fallGS9box = new System.Windows.Forms.TextBox();
            this.fallGS8box = new System.Windows.Forms.TextBox();
            this.fallGS7box = new System.Windows.Forms.TextBox();
            this.fallGS6box = new System.Windows.Forms.TextBox();
            this.fallGS5box = new System.Windows.Forms.TextBox();
            this.fallGS4box = new System.Windows.Forms.TextBox();
            this.fallGS3box = new System.Windows.Forms.TextBox();
            this.gpSumLabel = new System.Windows.Forms.Label();
            this.gpSumLabel2 = new System.Windows.Forms.Label();
            this.suspGS9box = new System.Windows.Forms.CheckBox();
            this.suspGS8box = new System.Windows.Forms.CheckBox();
            this.suspGS7box = new System.Windows.Forms.CheckBox();
            this.suspGS6box = new System.Windows.Forms.CheckBox();
            this.suspGS5box = new System.Windows.Forms.CheckBox();
            this.suspGS4box = new System.Windows.Forms.CheckBox();
            this.suspGS3box = new System.Windows.Forms.CheckBox();
            this.suspGS2box = new System.Windows.Forms.CheckBox();
            this.fallGS2box = new System.Windows.Forms.TextBox();
            this.fallGS1box = new System.Windows.Forms.TextBox();
            this.label28 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.suspGS1box = new System.Windows.Forms.CheckBox();
            this.gp3box = new System.Windows.Forms.TextBox();
            this.gp4box = new System.Windows.Forms.TextBox();
            this.gp5box = new System.Windows.Forms.TextBox();
            this.gp6box = new System.Windows.Forms.TextBox();
            this.gp7box = new System.Windows.Forms.TextBox();
            this.gp8box = new System.Windows.Forms.TextBox();
            this.gp9box = new System.Windows.Forms.TextBox();
            this.gp2box = new System.Windows.Forms.TextBox();
            this.gp1box = new System.Windows.Forms.TextBox();
            this.g3box = new System.Windows.Forms.TextBox();
            this.g4box = new System.Windows.Forms.TextBox();
            this.g5box = new System.Windows.Forms.TextBox();
            this.g6box = new System.Windows.Forms.TextBox();
            this.g7box = new System.Windows.Forms.TextBox();
            this.g8box = new System.Windows.Forms.TextBox();
            this.g9box = new System.Windows.Forms.TextBox();
            this.g2box = new System.Windows.Forms.TextBox();
            this.g1box = new System.Windows.Forms.TextBox();
            this.label22 = new System.Windows.Forms.Label();
            this.label21 = new System.Windows.Forms.Label();
            this.label20 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.label18 = new System.Windows.Forms.Label();
            this.label17 = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.DescriptionTab = new System.Windows.Forms.TabPage();
            this.DescBox = new System.Windows.Forms.TextBox();
            this.GridTab = new System.Windows.Forms.TabPage();
            this.overrideheaderBox = new System.Windows.Forms.CheckBox();
            this.dxbox = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.ytextbox = new System.Windows.Forms.TextBox();
            this.xtextbox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.HydrologyTab = new System.Windows.Forms.TabPage();
            this.groupBox7 = new System.Windows.Forms.GroupBox();
            this.label105 = new System.Windows.Forms.Label();
            this.mfiletimestepbox = new System.Windows.Forms.TextBox();
            this.hydroindexBox = new System.Windows.Forms.TextBox();
            this.label103 = new System.Windows.Forms.Label();
            this.label37 = new System.Windows.Forms.Label();
            this.mvaluebox = new System.Windows.Forms.TextBox();
            this.label102 = new System.Windows.Forms.Label();
            this.rfnumBox = new System.Windows.Forms.TextBox();
            this.checkBox7 = new System.Windows.Forms.CheckBox();
            this.label35 = new System.Windows.Forms.Label();
            this.raintimestepbox = new System.Windows.Forms.TextBox();
            this.jmeaninputfilebox = new System.Windows.Forms.CheckBox();
            this.label59 = new System.Windows.Forms.Label();
            this.mvalueloadbox = new System.Windows.Forms.TextBox();
            this.raindataloadbox = new System.Windows.Forms.TextBox();
            this.label25 = new System.Windows.Forms.Label();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.label83 = new System.Windows.Forms.Label();
            this.div_inputs_box = new System.Windows.Forms.TextBox();
            this.inboxExtra = new System.Windows.Forms.CheckBox();
            this.infileExtra = new System.Windows.Forms.TextBox();
            this.infile8 = new System.Windows.Forms.TextBox();
            this.ybox8 = new System.Windows.Forms.TextBox();
            this.xbox8 = new System.Windows.Forms.TextBox();
            this.inbox8 = new System.Windows.Forms.CheckBox();
            this.infile7 = new System.Windows.Forms.TextBox();
            this.ybox7 = new System.Windows.Forms.TextBox();
            this.xbox7 = new System.Windows.Forms.TextBox();
            this.inbox7 = new System.Windows.Forms.CheckBox();
            this.infile6 = new System.Windows.Forms.TextBox();
            this.ybox6 = new System.Windows.Forms.TextBox();
            this.xbox6 = new System.Windows.Forms.TextBox();
            this.inbox6 = new System.Windows.Forms.CheckBox();
            this.infile5 = new System.Windows.Forms.TextBox();
            this.ybox5 = new System.Windows.Forms.TextBox();
            this.xbox5 = new System.Windows.Forms.TextBox();
            this.inbox5 = new System.Windows.Forms.CheckBox();
            this.input_time_step_box = new System.Windows.Forms.TextBox();
            this.infile4 = new System.Windows.Forms.TextBox();
            this.infile3 = new System.Windows.Forms.TextBox();
            this.infile2 = new System.Windows.Forms.TextBox();
            this.infile1 = new System.Windows.Forms.TextBox();
            this.ybox1 = new System.Windows.Forms.TextBox();
            this.ybox2 = new System.Windows.Forms.TextBox();
            this.ybox3 = new System.Windows.Forms.TextBox();
            this.ybox4 = new System.Windows.Forms.TextBox();
            this.xbox2 = new System.Windows.Forms.TextBox();
            this.xbox3 = new System.Windows.Forms.TextBox();
            this.xbox4 = new System.Windows.Forms.TextBox();
            this.xbox1 = new System.Windows.Forms.TextBox();
            this.label29 = new System.Windows.Forms.Label();
            this.label44 = new System.Windows.Forms.Label();
            this.label43 = new System.Windows.Forms.Label();
            this.label41 = new System.Windows.Forms.Label();
            this.inbox2 = new System.Windows.Forms.CheckBox();
            this.inbox3 = new System.Windows.Forms.CheckBox();
            this.inbox4 = new System.Windows.Forms.CheckBox();
            this.inbox1 = new System.Windows.Forms.CheckBox();
            this.label42 = new System.Windows.Forms.Label();
            this.groupBoxWaterSourceTracer = new System.Windows.Forms.GroupBox();
            this.checkBoxSoluteTracer = new System.Windows.Forms.CheckBox();
            this.textBox20 = new System.Windows.Forms.TextBox();
            this.checkBox11 = new System.Windows.Forms.CheckBox();
            this.checkBox10 = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label90 = new System.Windows.Forms.Label();
            this.TidalFileName = new System.Windows.Forms.TextBox();
            this.TidalInputStep = new System.Windows.Forms.TextBox();
            this.label82 = new System.Windows.Forms.Label();
            this.TidalYmin = new System.Windows.Forms.TextBox();
            this.TidalYmax = new System.Windows.Forms.TextBox();
            this.TidalXmax = new System.Windows.Forms.TextBox();
            this.TidalXmin = new System.Windows.Forms.TextBox();
            this.label80 = new System.Windows.Forms.Label();
            this.label81 = new System.Windows.Forms.Label();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.groupBox8 = new System.Windows.Forms.GroupBox();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.veg_lat_box = new System.Windows.Forms.TextBox();
            this.label51 = new System.Windows.Forms.Label();
            this.grasstextbox = new System.Windows.Forms.TextBox();
            this.label40 = new System.Windows.Forms.Label();
            this.label36 = new System.Windows.Forms.Label();
            this.vegTauCritBox = new System.Windows.Forms.TextBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.angle_thresholdbox = new System.Windows.Forms.TextBox();
            this.soilerosionBox = new System.Windows.Forms.CheckBox();
            this.landslidesBox = new System.Windows.Forms.CheckBox();
            this.label75 = new System.Windows.Forms.Label();
            this.label74 = new System.Windows.Forms.Label();
            this.label73 = new System.Windows.Forms.Label();
            this.label72 = new System.Windows.Forms.Label();
            this.label71 = new System.Windows.Forms.Label();
            this.m3Box = new System.Windows.Forms.TextBox();
            this.n1Box = new System.Windows.Forms.TextBox();
            this.m1Box = new System.Windows.Forms.TextBox();
            this.Beta3Box = new System.Windows.Forms.TextBox();
            this.Beta1Box = new System.Windows.Forms.TextBox();
            this.SiberiaBox = new System.Windows.Forms.CheckBox();
            this.label70 = new System.Windows.Forms.Label();
            this.label69 = new System.Windows.Forms.Label();
            this.label68 = new System.Windows.Forms.Label();
            this.label67 = new System.Windows.Forms.Label();
            this.soil_ratebox = new System.Windows.Forms.TextBox();
            this.slopebox = new System.Windows.Forms.TextBox();
            this.creepratebox = new System.Windows.Forms.TextBox();
            this.label34 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.label91 = new System.Windows.Forms.Label();
            this.textBox10 = new System.Windows.Forms.TextBox();
            this.fraction_dune = new System.Windows.Forms.TextBox();
            this.label57 = new System.Windows.Forms.Label();
            this.dune_grid_size_box = new System.Windows.Forms.TextBox();
            this.label56 = new System.Windows.Forms.Label();
            this.dune_time_box = new System.Windows.Forms.TextBox();
            this.label89 = new System.Windows.Forms.Label();
            this.label88 = new System.Windows.Forms.Label();
            this.label87 = new System.Windows.Forms.Label();
            this.label86 = new System.Windows.Forms.Label();
            this.label85 = new System.Windows.Forms.Label();
            this.label84 = new System.Windows.Forms.Label();
            this.slab_depth_box = new System.Windows.Forms.TextBox();
            this.shadow_angle_box = new System.Windows.Forms.TextBox();
            this.upstream_check_box = new System.Windows.Forms.TextBox();
            this.depo_prob_box = new System.Windows.Forms.TextBox();
            this.offset_box = new System.Windows.Forms.TextBox();
            this.init_depth_box = new System.Windows.Forms.TextBox();
            this.DuneBox = new System.Windows.Forms.CheckBox();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.textBox19 = new System.Windows.Forms.TextBox();
            this.label104 = new System.Windows.Forms.Label();
            this.SpatVarManningsCheckbox = new System.Windows.Forms.CheckBox();
            this.MinQmaxvalue = new System.Windows.Forms.TextBox();
            this.textBox9 = new System.Windows.Forms.TextBox();
            this.label77 = new System.Windows.Forms.Label();
            this.textBox8 = new System.Windows.Forms.TextBox();
            this.label66 = new System.Windows.Forms.Label();
            this.textBox4 = new System.Windows.Forms.TextBox();
            this.label64 = new System.Windows.Forms.Label();
            this.courantbox = new System.Windows.Forms.TextBox();
            this.label38 = new System.Windows.Forms.Label();
            this.label53 = new System.Windows.Forms.Label();
            this.Q2box = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.k_evapBox = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.label46 = new System.Windows.Forms.Label();
            this.minqbox = new System.Windows.Forms.TextBox();
            this.initscansbox = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.label101 = new System.Windows.Forms.Label();
            this.label100 = new System.Windows.Forms.Label();
            this.label99 = new System.Windows.Forms.Label();
            this.textBox18 = new System.Windows.Forms.TextBox();
            this.textBox17 = new System.Windows.Forms.TextBox();
            this.textBox16 = new System.Windows.Forms.TextBox();
            this.label98 = new System.Windows.Forms.Label();
            this.label97 = new System.Windows.Forms.Label();
            this.label96 = new System.Windows.Forms.Label();
            this.textBox15 = new System.Windows.Forms.TextBox();
            this.textBox14 = new System.Windows.Forms.TextBox();
            this.textBox13 = new System.Windows.Forms.TextBox();
            this.label95 = new System.Windows.Forms.Label();
            this.label94 = new System.Windows.Forms.Label();
            this.textBox12 = new System.Windows.Forms.TextBox();
            this.textBox11 = new System.Windows.Forms.TextBox();
            this.checkBox6 = new System.Windows.Forms.CheckBox();
            this.checkBox5 = new System.Windows.Forms.CheckBox();
            this.checkBox4 = new System.Windows.Forms.CheckBox();
            this.soildevbox = new System.Windows.Forms.CheckBox();
            this.OilTab = new System.Windows.Forms.TabPage();
            this.OilTab_checkBox = new System.Windows.Forms.CheckBox();
            this.OilTab_groupBox_controls = new System.Windows.Forms.GroupBox();
            this.OilYmin = new System.Windows.Forms.TextBox();
            this.OilYmax = new System.Windows.Forms.TextBox();
            //this.OilSpillDuration = new System.Windows.Forms.TextBox();
            this.OilXmin = new System.Windows.Forms.TextBox();
            this.OilXmax = new System.Windows.Forms.TextBox();
            this.OilYlabel = new System.Windows.Forms.Label();
            this.OilXlabel = new System.Windows.Forms.Label();
            this.OilTextlabel = new System.Windows.Forms.Label();
            this.OilTimeMin = new System.Windows.Forms.TextBox();
            this.OilTimeMinlabel = new System.Windows.Forms.Label();
            this.OilDepthStart = new System.Windows.Forms.TextBox();
            this.OilDepthStartlabel = new System.Windows.Forms.Label();
            this.OilVolume = new System.Windows.Forms.TextBox();
            this.OilVolumelabel = new System.Windows.Forms.Label();
            this.OilSpillDuration = new System.Windows.Forms.TextBox();
            this.OilSpillDurationlabel = new System.Windows.Forms.Label();
            this.TempTab = new System.Windows.Forms.TabPage(); // TEMP_V1
            this.TempTab_checkBox = new System.Windows.Forms.CheckBox(); // TEMP_V1
            this.TempTab_label_airtemp = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_airtemp = new System.Windows.Forms.TextBox(); // TEMP_V1
            this.TempTab_label_shortwave = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_shortwave = new System.Windows.Forms.TextBox(); // TEMP_V1
            this.TempTab_label_windspeed = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_windspeed = new System.Windows.Forms.TextBox(); // TEMP_V1
            this.TempTab_label_humidity = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_humidity = new System.Windows.Forms.TextBox(); // TEMP_V1
            this.TempTab_label_cloudcover = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_cloudcover = new System.Windows.Forms.TextBox(); // TEMP_V1
            this.TempTab_label_pressure = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_pressure = new System.Windows.Forms.TextBox(); // TEMP_V1
            this.TempTab_label_dewpoint = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_dewpoint = new System.Windows.Forms.TextBox(); // TEMP_V1

            this.TempTab_groupBox_scheme = new System.Windows.Forms.GroupBox();
            this.TempTab_radio_fullscheme = new System.Windows.Forms.RadioButton();
            this.TempTab_radio_simplifiedscheme = new System.Windows.Forms.RadioButton();
            this.TempTab_checkBox_hecraslongwave = new System.Windows.Forms.CheckBox();
            this.TempTab_checkBox_hecrasalbedo = new System.Windows.Forms.CheckBox();
            this.TempTab_label_thermalinterval = new System.Windows.Forms.Label();
            this.TempTab_textBox_thermalinterval = new System.Windows.Forms.TextBox();
            this.TempTab_label_mettimestep = new System.Windows.Forms.Label();
            this.TempTab_textBox_mettimestep = new System.Windows.Forms.TextBox();

            this.TempTab_groupBox_site = new System.Windows.Forms.GroupBox();
            this.TempTab_label_latitude = new System.Windows.Forms.Label();
            this.TempTab_textBox_latitude = new System.Windows.Forms.TextBox();
            this.TempTab_label_longitude = new System.Windows.Forms.Label();
            this.TempTab_textBox_longitude = new System.Windows.Forms.TextBox();
            this.TempTab_label_timezone = new System.Windows.Forms.Label();
            this.TempTab_textBox_timezone = new System.Windows.Forms.TextBox();
            this.TempTab_label_elevation = new System.Windows.Forms.Label();
            this.TempTab_textBox_elevation = new System.Windows.Forms.TextBox();
            this.TempTab_label_windheight = new System.Windows.Forms.Label();
            this.TempTab_textBox_windheight = new System.Windows.Forms.TextBox();
            this.TempTab_label_startdate = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_startdate = new System.Windows.Forms.TextBox(); // TEMP_V1

            this.TempTab_groupBox_initial = new System.Windows.Forms.GroupBox();
            this.TempTab_label_initialtemp = new System.Windows.Forms.Label();
            this.TempTab_textBox_initialtemp = new System.Windows.Forms.TextBox();
            this.TempTab_label_initialraster = new System.Windows.Forms.Label();
            this.TempTab_textBox_initialraster = new System.Windows.Forms.TextBox();
            this.TempTab_label_sourcetemp = new System.Windows.Forms.Label(); // TEMP_V1
            this.TempTab_textBox_sourcetemp = new System.Windows.Forms.TextBox(); // TEMP_V1

            this.folderBrowserOutDir = new System.Windows.Forms.FolderBrowserDialog();
            this.label107 = new System.Windows.Forms.Label();
            this.label106 = new System.Windows.Forms.Label();
            this.label110 = new System.Windows.Forms.Label();
            this.label109 = new System.Windows.Forms.Label();
            this.label108 = new System.Windows.Forms.Label();
            this.Panel1 = new System.Windows.Forms.Panel();
            this.tempdata2 = new System.Windows.Forms.TextBox();
            this.tempdata1 = new System.Windows.Forms.TextBox();
            this.graphicToGoogleEarthButton = new System.Windows.Forms.Button();
            this.button4 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.recirculatebox = new System.Windows.Forms.CheckBox();
            this.flowonlybox = new System.Windows.Forms.CheckBox();
            this.statusBar1 = new System.Windows.Forms.StatusBar();
            this.InfoStatusPanel = new System.Windows.Forms.StatusBarPanel();
            this.IterationStatusPanel = new System.Windows.Forms.StatusBarPanel();
            this.TimeStatusPanel = new System.Windows.Forms.StatusBarPanel();
            this.QwStatusPanel = new System.Windows.Forms.StatusBarPanel();
            this.QsStatusPanel = new System.Windows.Forms.StatusBarPanel();
            this.tempStatusPanel = new System.Windows.Forms.StatusBarPanel();
            this.start_button = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            checkBox2 = new System.Windows.Forms.CheckBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.trackBar1 = new System.Windows.Forms.TrackBar();
            this.label61 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label62 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.label63 = new System.Windows.Forms.Label();
            this.trackBar2 = new System.Windows.Forms.TrackBar();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.zoomPanImageBox1 = new Smallwisdom.Windows.Forms.ZoomPanImageBox();
            this.groupBox9 = new System.Windows.Forms.GroupBox();
            this.trackBar3 = new System.Windows.Forms.TrackBar();
            this.checkBox12 = new System.Windows.Forms.CheckBox();
            this.checkBoxSoluteVis = new System.Windows.Forms.CheckBox();
            this.comboBox4 = new System.Windows.Forms.ComboBox();
            this.comboBox3 = new System.Windows.Forms.ComboBox();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.tabControl1.SuspendLayout();
            this.FilesTab.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBoxOutputDirectory.SuspendLayout();
            this.NumericalTab.SuspendLayout();
            this.GrainTab.SuspendLayout();
            this.DescriptionTab.SuspendLayout();
            this.GridTab.SuspendLayout();
            this.HydrologyTab.SuspendLayout();
            this.groupBox7.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBoxWaterSourceTracer.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.groupBox8.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.OilTab.SuspendLayout();
            this.OilTab_groupBox_controls.SuspendLayout();
            this.Panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InfoStatusPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.IterationStatusPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.TimeStatusPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.QwStatusPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.QsStatusPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tempStatusPanel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar1)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar2)).BeginInit();
            this.groupBox9.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar3)).BeginInit();
            this.SuspendLayout();
            //
            // button2
            //
            this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button2.Location = new System.Drawing.Point(9, 530);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(100, 25);
            this.button2.TabIndex = 7;
            this.button2.Text = "load data";
            this.button2.Click += new System.EventHandler(this.button2_Click);
            //
            // mainMenu1
            //
            this.mainMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemConfigFile,
            this.menuItem1,
            this.menuItem2,
            this.menuItem11});
            //
            // menuItemConfigFile
            //
            this.menuItemConfigFile.Index = 0;
            this.menuItemConfigFile.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItemConfigFileOpen,
            this.menuItemConfigFileSaveAs,
            this.menuItemConfigFileSave});
            this.menuItemConfigFile.Text = "Config &File";
            //
            // menuItemConfigFileOpen
            //
            this.menuItemConfigFileOpen.Index = 0;
            this.menuItemConfigFileOpen.Text = "&Open";
            this.menuItemConfigFileOpen.Click += new System.EventHandler(this.menuItemConfigFileOpen_Click);
            //
            // menuItemConfigFileSaveAs
            //
            this.menuItemConfigFileSaveAs.Index = 1;
            this.menuItemConfigFileSaveAs.Text = "Save &As";
            this.menuItemConfigFileSaveAs.Click += new System.EventHandler(this.menuItemConfigFileSave_Click);
            //
            // menuItemConfigFileSave
            //
            this.menuItemConfigFileSave.Index = 2;
            this.menuItemConfigFileSave.Text = "&Save";
            this.menuItemConfigFileSave.Click += new System.EventHandler(this.menuItemConfigFileSave_Click);
            //
            // menuItem1
            //
            this.menuItem1.Index = 1;
            this.menuItem1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem30,
            this.menuItem3,
            this.menuItem4,
            this.menuItem5,
            this.menuItem10,
            this.menuItemOilVisualisation,
            this.menuItemOilconcVisualisation});
            this.menuItem1.Text = "&Top Graphics";
            //
            // menuItem30
            //
            this.menuItem30.Index = 0;
            this.menuItem30.Text = "DEM";
            this.menuItem30.Click += new System.EventHandler(this.menuItem30_Click);
            //
            // menuItem3
            //
            this.menuItem3.Checked = true;
            this.menuItem3.Index = 1;
            this.menuItem3.Text = "water depth (low scale)";
            this.menuItem3.Click += new System.EventHandler(this.menuItem3_Click);
            //
            // menuItem4
            //
            this.menuItem4.Index = 2;
            this.menuItem4.Text = "erosion/dep";
            this.menuItem4.Click += new System.EventHandler(this.menuItem4_Click);
            //
            // menuItem5
            //
            this.menuItem5.Index = 3;
            this.menuItem5.Text = "Grass/veg cover";
            this.menuItem5.Click += new System.EventHandler(this.menuItem5_Click);
            //
            // menuItem10
            //
            this.menuItem10.Index = 4;
            this.menuItem10.Text = "water source tracer";
            this.menuItem10.Click += new System.EventHandler(this.menuItem10_Click);
            //
            // menuItem2
            //
            this.menuItem2.Index = 2;
            this.menuItem2.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem7,
            this.menuItem8,
            this.menuItem9,
            this.menuItem26,
            this.menuItem27,
            this.menuItem28,
            this.menuItem31,
            this.menuItemOilVisualisation,
            this.menuItemOilconcVisualisation,
            this.menuItemWaterTempVisualisation});
            this.menuItem2.Text = "&Top graphics II";
            //
            // menuItem7
            //
            this.menuItem7.Index = 0;
            this.menuItem7.Text = "lateral gradient";
            this.menuItem7.Click += new System.EventHandler(this.menuItem7_Click);
            //
            // menuItem8
            //
            this.menuItem8.Index = 1;
            this.menuItem8.Text = "Bed sheer stress";
            this.menuItem8.Click += new System.EventHandler(this.menuItem8_Click);
            //
            // menuItem9
            //
            this.menuItem9.Index = 2;
            this.menuItem9.Text = "grainsize (new scale)";
            this.menuItem9.Click += new System.EventHandler(this.menuItem9_Click);
            //
            // menuItem26
            //
            this.menuItem26.Index = 3;
            this.menuItem26.Text = "Drainage area";
            this.menuItem26.Click += new System.EventHandler(this.menuItem26_Click);
            //
            // menuItem27
            //
            this.menuItem27.Index = 4;
            this.menuItem27.Text = "susp conc";
            this.menuItem27.Click += new System.EventHandler(this.menuItem27_Click);
            //
            // menuItem28
            //
            this.menuItem28.Index = 5;
            this.menuItem28.Text = "soil depth";
            this.menuItem28.Click += new System.EventHandler(this.menuItem28_Click);
            //
            // menuItem31
            //
            this.menuItem31.Index = 6;
            this.menuItem31.Text = "flow velocity";
            this.menuItem31.Click += new System.EventHandler(this.menuItem31_Click);
            //
            // menuItemOilVisualisation OIL_V1_MDW
            //
            this.menuItemOilVisualisation.Index = 7;
            this.menuItemOilVisualisation.Text = "oil spill";
            this.menuItemOilVisualisation.Click += new System.EventHandler(this.menuItemOilVisualisation_Click);
            //
            // menuItemOilconcVisualisation OIL_V1_PDF
            //
            this.menuItemOilconcVisualisation.Index = 8;
            this.menuItemOilconcVisualisation.Text = "oil concentration";
            this.menuItemOilconcVisualisation.Click += new System.EventHandler(this.menuItemOilconcVisualisation_Click);
            //
            // menuItemWaterTempVisualisation TEMP_V1
            //
            this.menuItemWaterTempVisualisation.Index = 9;
            this.menuItemWaterTempVisualisation.Text = "water temperature";
            this.menuItemWaterTempVisualisation.Click += new System.EventHandler(this.menuItemWaterTempVisualisation_Click);
            //
            // menuItem11
            //
            this.menuItem11.Index = 3;
            this.menuItem11.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
            this.menuItem12,
            this.menuItem13,
            this.menuItem14,
            this.menuItem25,
            this.menuItem29,
            this.menuItem33,
            this.menuItem34,
            this.menuItem6,
            this.menuItem15,
            this.menuItem16,
            this.menuItemSoluteTracer,
            this.menuItemOilSpill,
            this.menuItem17,
            this.menuItemOilconc});
            //OIL_V1_PDF
            this.menuItem11.Text = "Save Options";
            //
            // menuItem12
            //
            this.menuItem12.Checked = true;
            this.menuItem12.Index = 0;
            this.menuItem12.Text = "elevations";
            this.menuItem12.Click += new System.EventHandler(this.menuItem12_Click);
            //
            // menuItem13
            //
            this.menuItem13.Checked = true;
            this.menuItem13.Index = 1;
            this.menuItem13.Text = "elev diff";
            this.menuItem13.Click += new System.EventHandler(this.menuItem13_Click);
            //
            // menuItem14
            //
            this.menuItem14.Checked = true;
            this.menuItem14.Index = 2;
            this.menuItem14.Text = "grainsize";
            this.menuItem14.Click += new System.EventHandler(this.menuItem14_Click);
            //
            // menuItem25
            //
            this.menuItem25.Index = 3;
            this.menuItem25.Text = "water depth";
            this.menuItem25.Click += new System.EventHandler(this.menuItem25_Click);
            //
            // menuItem29
            //
            this.menuItem29.Checked = true;
            this.menuItem29.Index = 4;
            this.menuItem29.Text = "d50 top layer";
            this.menuItem29.Click += new System.EventHandler(this.menuItem29_Click);
            //
            // menuItem33
            //
            this.menuItem33.Index = 5;
            this.menuItem33.Text = "flow velocity";
            this.menuItem33.Click += new System.EventHandler(this.menuItem33_Click);
            //
            // menuItem34
            //
            this.menuItem34.Index = 6;
            this.menuItem34.Text = "Veloc vectors";
            this.menuItem34.Click += new System.EventHandler(this.menuItem34_Click);
            //
            // menuItem6
            //
            this.menuItem6.Index = 7;
            this.menuItem6.Text = "water tracers";
            this.menuItem6.Click += new System.EventHandler(this.menuItem6_Click);
            //
            // menuItem15
            //
            this.menuItem15.Index = 8;
            this.menuItem15.Text = "rain zone tracers";
            this.menuItem15.Click += new System.EventHandler(this.menuItem15_Click);
            //
            // menuItem16
            //
            this.menuItem16.Index = 9;
            this.menuItem16.Text = "sediment tracers";
            this.menuItem16.Click += new System.EventHandler(this.menuItem16_Click);
            //
            // menuItemSoluteTracer
            //
            this.menuItemSoluteTracer.Index = 10;
            this.menuItemSoluteTracer.Text = "solute tracers";
            this.menuItemSoluteTracer.Click += new System.EventHandler(this.menuItemSoluteTracer_Click);
            //
            //menuItemOilSpill
            // 
            //OILV1_PDF
            //
            this.menuItemOilSpill.Index = 11;
            this.menuItemOilSpill.Text = "oil depth";
            this.menuItemOilSpill.Click += new System.EventHandler(this.menuItemOilSpill_Click);
            // 
            //menuItem17
            // 
            //OIL_V1_PDF
            //
            this.menuItem17.Index = 12;
            this.menuItem17.Text = "mass balance";
            this.menuItem17.Click += new System.EventHandler(this.menuItem17_Click);
            // 
            //menuItemOilconc
            // 
            //OIL_V1_PDF
            //
            this.menuItemOilconc.Index = 13;
            this.menuItemOilconc.Text = "oil concentration";
            this.menuItemOilconc.Click += new System.EventHandler(this.menuItemOilconc_Click);
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.FilesTab);
            this.tabControl1.Controls.Add(this.NumericalTab);
            this.tabControl1.Controls.Add(this.GrainTab);
            this.tabControl1.Controls.Add(this.DescriptionTab);
            this.tabControl1.Controls.Add(this.GridTab);
            this.tabControl1.Controls.Add(this.HydrologyTab);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.OilTab);
            this.tabControl1.Controls.Add(this.TempTab); // TEMP_V1
            this.tabControl1.Location = new System.Drawing.Point(6, 1);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1331, 530);
            this.tabControl1.TabIndex = 143;
            this.tabControl1.Tag = "Flow Model";
            //
            // FilesTab
            //
            this.FilesTab.Controls.Add(this.label123);
            this.FilesTab.Controls.Add(this.label124);
            this.FilesTab.Controls.Add(this.label122);
            this.FilesTab.Controls.Add(this.grain_index_file);
            this.FilesTab.Controls.Add(this.label121);
            this.FilesTab.Controls.Add(this.tracer_file);
            this.FilesTab.Controls.Add(this.tracer_num);
            this.FilesTab.Controls.Add(this.checkBox_tracer);
            this.FilesTab.Controls.Add(this.groupBox6);
            this.FilesTab.Controls.Add(this.checkBox3);
            this.FilesTab.Controls.Add(this.outputfilesaveintervalbox);
            this.FilesTab.Controls.Add(this.TimeseriesOutBox);
            this.FilesTab.Controls.Add(this.tracerOutputtextBox);
            this.FilesTab.Controls.Add(this.tracerOutcheckBox);
            this.FilesTab.Controls.Add(this.reach_mode_box);
            this.FilesTab.Controls.Add(this.catchment_mode_box);
            this.FilesTab.Controls.Add(this.checkBoxGenerateTimeSeries);
            this.FilesTab.Controls.Add(this.checkBoxGenerateIterations);
            this.FilesTab.Controls.Add(this.IterationOutbox);
            this.FilesTab.Controls.Add(this.saveintervalbox);
            this.FilesTab.Controls.Add(this.label45);
            this.FilesTab.Controls.Add(this.label33);
            this.FilesTab.Controls.Add(this.uniquefilecheck);
            this.FilesTab.Controls.Add(this.tracerhydrofile);
            this.FilesTab.Controls.Add(this.mine_input_textBox);
            this.FilesTab.Controls.Add(this.bedrockbox);
            this.FilesTab.Controls.Add(this.graindataloadbox);
            this.FilesTab.Controls.Add(this.openfiletextbox);
            this.FilesTab.Controls.Add(this.tracerbox);
            this.FilesTab.Controls.Add(this.label39);
            this.FilesTab.Controls.Add(this.label24);
            this.FilesTab.Controls.Add(this.label23);
            this.FilesTab.Controls.Add(this.groupBoxOutputDirectory);
            this.FilesTab.Location = new System.Drawing.Point(4, 22);
            this.FilesTab.Name = "FilesTab";
            this.FilesTab.Size = new System.Drawing.Size(1323, 504);
            this.FilesTab.TabIndex = 0;
            this.FilesTab.Text = "Files";
            this.FilesTab.UseVisualStyleBackColor = true;
            //
            // label123
            //
            this.label123.AutoSize = true;
            this.label123.Location = new System.Drawing.Point(333, 136);
            this.label123.Name = "label123";
            this.label123.Size = new System.Drawing.Size(66, 13);
            this.label123.TabIndex = 216;
            this.label123.Text = "minesites file";
            this.label123.Visible = false;
            //
            // label124
            //
            this.label124.AutoSize = true;
            this.label124.Location = new System.Drawing.Point(333, 110);
            this.label124.Name = "label124";
            this.label124.Size = new System.Drawing.Size(82, 13);
            this.label124.TabIndex = 215;
            this.label124.Text = "Tracer index file";
            this.label124.Visible = false;
            //
            // label122
            //
            this.label122.AutoSize = true;
            this.label122.Location = new System.Drawing.Point(333, 84);
            this.label122.Name = "label122";
            this.label122.Size = new System.Drawing.Size(91, 13);
            this.label122.TabIndex = 213;
            this.label122.Text = "Number of tracers";
            this.label122.Visible = false;
            //
            // grain_index_file
            //
            this.grain_index_file.Location = new System.Drawing.Point(131, 127);
            this.grain_index_file.Name = "grain_index_file";
            this.grain_index_file.Size = new System.Drawing.Size(120, 20);
            this.grain_index_file.TabIndex = 212;
            this.grain_index_file.Text = "null";
            //
            // label121
            //
            this.label121.AutoSize = true;
            this.label121.Location = new System.Drawing.Point(47, 130);
            this.label121.Name = "label121";
            this.label121.Size = new System.Drawing.Size(76, 13);
            this.label121.TabIndex = 211;
            this.label121.Text = "Grain index file";
            //
            // tracer_file
            //
            this.tracer_file.Location = new System.Drawing.Point(430, 107);
            this.tracer_file.Name = "tracer_file";
            this.tracer_file.Size = new System.Drawing.Size(100, 20);
            this.tracer_file.TabIndex = 210;
            this.tracer_file.Text = "null";
            this.tracer_file.Visible = false;
            this.tracer_file.TextChanged += new System.EventHandler(this.tracer_file_TextChanged);
            //
            // tracer_num
            //
            this.tracer_num.Location = new System.Drawing.Point(430, 81);
            this.tracer_num.Name = "tracer_num";
            this.tracer_num.Size = new System.Drawing.Size(100, 20);
            this.tracer_num.TabIndex = 209;
            this.tracer_num.Text = "0";
            this.tracer_num.Visible = false;
            //
            // checkBox_tracer
            //
            this.checkBox_tracer.AutoSize = true;
            this.checkBox_tracer.Location = new System.Drawing.Point(392, 54);
            this.checkBox_tracer.Name = "checkBox_tracer";
            this.checkBox_tracer.Size = new System.Drawing.Size(100, 17);
            this.checkBox_tracer.TabIndex = 206;
            this.checkBox_tracer.Text = "Sediment tracer";
            this.checkBox_tracer.UseVisualStyleBackColor = true;
            this.checkBox_tracer.CheckedChanged += new System.EventHandler(this.checkBox_tracer_CheckedChanged);
            //
            // groupBox6
            //
            this.groupBox6.Controls.Add(this.textBox21);
            this.groupBox6.Controls.Add(this.groupBox4);
            this.groupBox6.Controls.Add(this.UTMgridcheckbox);
            this.groupBox6.Controls.Add(this.textBox5);
            this.groupBox6.Controls.Add(this.googleBeginDate);
            this.groupBox6.Controls.Add(this.label78);
            this.groupBox6.Controls.Add(this.googAnimationSaveInterval);
            this.groupBox6.Controls.Add(this.label79);
            this.groupBox6.Controls.Add(this.googleAnimationTextBox);
            this.groupBox6.Controls.Add(this.googleAnimationCheckbox);
            this.groupBox6.Location = new System.Drawing.Point(600, 192);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(418, 236);
            this.groupBox6.TabIndex = 205;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Google Earth output variables";
            //
            // textBox21
            //
            this.textBox21.Location = new System.Drawing.Point(940, 180);
            this.textBox21.Name = "textBox21";
            this.textBox21.Size = new System.Drawing.Size(100, 20);
            this.textBox21.TabIndex = 210;
            //
            // groupBox4
            //
            this.groupBox4.Controls.Add(this.textBox6);
            this.groupBox4.Controls.Add(this.UTMsouthcheck);
            this.groupBox4.Controls.Add(this.UTMzonebox);
            this.groupBox4.Location = new System.Drawing.Point(220, 137);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(144, 65);
            this.groupBox4.TabIndex = 201;
            this.groupBox4.TabStop = false;
            this.groupBox4.Visible = false;
            //
            // textBox6
            //
            this.textBox6.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox6.Location = new System.Drawing.Point(6, 17);
            this.textBox6.Multiline = true;
            this.textBox6.Name = "textBox6";
            this.textBox6.ReadOnly = true;
            this.textBox6.Size = new System.Drawing.Size(87, 22);
            this.textBox6.TabIndex = 196;
            this.textBox6.Text = "UTM zone (1-60)";
            this.textBox6.Visible = false;
            //
            // UTMsouthcheck
            //
            this.UTMsouthcheck.AutoSize = true;
            this.UTMsouthcheck.Location = new System.Drawing.Point(6, 42);
            this.UTMsouthcheck.Name = "UTMsouthcheck";
            this.UTMsouthcheck.Size = new System.Drawing.Size(128, 17);
            this.UTMsouthcheck.TabIndex = 197;
            this.UTMsouthcheck.Text = "Southern Hemisphere";
            this.UTMsouthcheck.UseVisualStyleBackColor = true;
            this.UTMsouthcheck.Visible = false;
            //
            // UTMzonebox
            //
            this.UTMzonebox.Location = new System.Drawing.Point(99, 16);
            this.UTMzonebox.Name = "UTMzonebox";
            this.UTMzonebox.Size = new System.Drawing.Size(39, 20);
            this.UTMzonebox.TabIndex = 194;
            this.UTMzonebox.Tag = "UTM zone";
            this.toolTip1.SetToolTip(this.UTMzonebox, "Enter the UTM zone");
            this.UTMzonebox.Visible = false;
            //
            // UTMgridcheckbox
            //
            this.UTMgridcheckbox.AutoSize = true;
            this.UTMgridcheckbox.Location = new System.Drawing.Point(225, 116);
            this.UTMgridcheckbox.Name = "UTMgridcheckbox";
            this.UTMgridcheckbox.Size = new System.Drawing.Size(82, 17);
            this.UTMgridcheckbox.TabIndex = 200;
            this.UTMgridcheckbox.Text = "Grid is UTM";
            this.UTMgridcheckbox.UseVisualStyleBackColor = true;
            this.UTMgridcheckbox.CheckedChanged += new System.EventHandler(this.UTMgridcheckbox_CheckedChanged);
            //
            // textBox5
            //
            this.textBox5.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBox5.Location = new System.Drawing.Point(26, 112);
            this.textBox5.Multiline = true;
            this.textBox5.Name = "textBox5";
            this.textBox5.ReadOnly = true;
            this.textBox5.Size = new System.Drawing.Size(190, 35);
            this.textBox5.TabIndex = 199;
            this.textBox5.Text = "For DTMs using British National Grid or UTM WGS84";
            //
            // googleBeginDate
            //
            this.googleBeginDate.AcceptsTab = true;
            this.googleBeginDate.Location = new System.Drawing.Point(222, 84);
            this.googleBeginDate.Name = "googleBeginDate";
            this.googleBeginDate.Size = new System.Drawing.Size(106, 20);
            this.googleBeginDate.TabIndex = 191;
            //
            // label78
            //
            this.label78.AutoSize = true;
            this.label78.Location = new System.Drawing.Point(96, 87);
            this.label78.Name = "label78";
            this.label78.Size = new System.Drawing.Size(120, 13);
            this.label78.TabIndex = 190;
            this.label78.Text = "begin date (yyyy-mm-dd)";
            //
            // googAnimationSaveInterval
            //
            this.googAnimationSaveInterval.Location = new System.Drawing.Point(222, 58);
            this.googAnimationSaveInterval.Name = "googAnimationSaveInterval";
            this.googAnimationSaveInterval.Size = new System.Drawing.Size(56, 20);
            this.googAnimationSaveInterval.TabIndex = 188;
            this.googAnimationSaveInterval.Text = "1000";
            //
            // label79
            //
            this.label79.Location = new System.Drawing.Point(64, 58);
            this.label79.Name = "label79";
            this.label79.Size = new System.Drawing.Size(152, 25);
            this.label79.TabIndex = 189;
            this.label79.Text = "Save file every * mins";
            this.label79.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label79, "How often the avi file AND the other data files are saved");
            //
            // googleAnimationTextBox
            //
            this.googleAnimationTextBox.Location = new System.Drawing.Point(222, 32);
            this.googleAnimationTextBox.Name = "googleAnimationTextBox";
            this.googleAnimationTextBox.Size = new System.Drawing.Size(106, 20);
            this.googleAnimationTextBox.TabIndex = 185;
            this.googleAnimationTextBox.Text = "animation.kmz";
            this.toolTip1.SetToolTip(this.googleAnimationTextBox, "File name for avi file");
            //
            // googleAnimationCheckbox
            //
            this.googleAnimationCheckbox.Location = new System.Drawing.Point(27, 31);
            this.googleAnimationCheckbox.Name = "googleAnimationCheckbox";
            this.googleAnimationCheckbox.Size = new System.Drawing.Size(195, 24);
            this.googleAnimationCheckbox.TabIndex = 184;
            this.googleAnimationCheckbox.Text = "Generate Google Earth Animation";
            this.toolTip1.SetToolTip(this.googleAnimationCheckbox, "Check to generate a movie file of the screen display");
            //
            // checkBox3
            //
            this.checkBox3.Location = new System.Drawing.Point(307, 5);
            this.checkBox3.Name = "checkBox3";
            this.checkBox3.Size = new System.Drawing.Size(147, 42);
            this.checkBox3.TabIndex = 204;
            this.checkBox3.Text = "Stage/Tidal input";
            this.toolTip1.SetToolTip(this.checkBox3, "CAESAR can run in both catchment and reach mode combined. But if you have reach m" +
        "ode selected, you should enter an input file in the hydrology tab");
            //
            // outputfilesaveintervalbox
            //
            this.outputfilesaveintervalbox.Location = new System.Drawing.Point(208, 327);
            this.outputfilesaveintervalbox.Name = "outputfilesaveintervalbox";
            this.outputfilesaveintervalbox.Size = new System.Drawing.Size(56, 20);
            this.outputfilesaveintervalbox.TabIndex = 166;
            this.outputfilesaveintervalbox.Text = "60";
            //
            // TimeseriesOutBox
            //
            this.TimeseriesOutBox.Location = new System.Drawing.Point(208, 303);
            this.TimeseriesOutBox.Name = "TimeseriesOutBox";
            this.TimeseriesOutBox.Size = new System.Drawing.Size(112, 20);
            this.TimeseriesOutBox.TabIndex = 168;
            this.TimeseriesOutBox.Text = "catchment.dat";
            //
            // tracerOutputtextBox
            //
            this.tracerOutputtextBox.Location = new System.Drawing.Point(743, 14);
            this.tracerOutputtextBox.Name = "tracerOutputtextBox";
            this.tracerOutputtextBox.Size = new System.Drawing.Size(112, 20);
            this.tracerOutputtextBox.TabIndex = 181;
            this.tracerOutputtextBox.Text = "tracer_output.txt";
            this.tracerOutputtextBox.Visible = false;
            //
            // tracerOutcheckBox
            //
            this.tracerOutcheckBox.Location = new System.Drawing.Point(979, 11);
            this.tracerOutcheckBox.Name = "tracerOutcheckBox";
            this.tracerOutcheckBox.Size = new System.Drawing.Size(160, 24);
            this.tracerOutcheckBox.TabIndex = 180;
            this.tracerOutcheckBox.Text = "Generate tracer output";
            this.tracerOutcheckBox.Visible = false;
            //
            // reach_mode_box
            //
            this.reach_mode_box.Checked = true;
            this.reach_mode_box.CheckState = System.Windows.Forms.CheckState.Checked;
            this.reach_mode_box.Location = new System.Drawing.Point(196, 5);
            this.reach_mode_box.Name = "reach_mode_box";
            this.reach_mode_box.Size = new System.Drawing.Size(147, 42);
            this.reach_mode_box.TabIndex = 175;
            this.reach_mode_box.Text = "Reach Mode";
            this.toolTip1.SetToolTip(this.reach_mode_box, "CAESAR can run in both catchment and reach mode combined. But if you have reach m" +
        "ode selected, you should enter an input file in the hydrology tab");
            //
            // catchment_mode_box
            //
            this.catchment_mode_box.Location = new System.Drawing.Point(54, 9);
            this.catchment_mode_box.Name = "catchment_mode_box";
            this.catchment_mode_box.Size = new System.Drawing.Size(147, 34);
            this.catchment_mode_box.TabIndex = 174;
            this.catchment_mode_box.Text = "Catchment mode";
            this.toolTip1.SetToolTip(this.catchment_mode_box, "CAESAR can run in both catchment and reach mode, but if you have catchment mode c" +
        "hecked, you should input a rainfall data file");
            this.catchment_mode_box.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            //
            // checkBoxGenerateTimeSeries
            //
            this.checkBoxGenerateTimeSeries.Location = new System.Drawing.Point(44, 304);
            this.checkBoxGenerateTimeSeries.Name = "checkBoxGenerateTimeSeries";
            this.checkBoxGenerateTimeSeries.Size = new System.Drawing.Size(170, 24);
            this.checkBoxGenerateTimeSeries.TabIndex = 173;
            this.checkBoxGenerateTimeSeries.Text = "Generate time series output";
            //
            // checkBoxGenerateIterations
            //
            this.checkBoxGenerateIterations.Location = new System.Drawing.Point(45, 367);
            this.checkBoxGenerateIterations.Name = "checkBoxGenerateIterations";
            this.checkBoxGenerateIterations.Size = new System.Drawing.Size(152, 24);
            this.checkBoxGenerateIterations.TabIndex = 172;
            this.checkBoxGenerateIterations.Text = "Generate iteration output";
            this.checkBoxGenerateIterations.Visible = false;
            //
            // IterationOutbox
            //
            this.IterationOutbox.Location = new System.Drawing.Point(208, 367);
            this.IterationOutbox.Name = "IterationOutbox";
            this.IterationOutbox.Size = new System.Drawing.Size(112, 20);
            this.IterationOutbox.TabIndex = 170;
            this.IterationOutbox.Text = "iterout.dat";
            this.IterationOutbox.Visible = false;
            //
            // saveintervalbox
            //
            this.saveintervalbox.Location = new System.Drawing.Point(175, 224);
            this.saveintervalbox.Name = "saveintervalbox";
            this.saveintervalbox.Size = new System.Drawing.Size(56, 20);
            this.saveintervalbox.TabIndex = 163;
            this.saveintervalbox.Text = "1000";
            //
            // label45
            //
            this.label45.Location = new System.Drawing.Point(48, 327);
            this.label45.Name = "label45";
            this.label45.Size = new System.Drawing.Size(152, 24);
            this.label45.TabIndex = 167;
            this.label45.Text = "Save file every * mins";
            this.label45.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label33
            //
            this.label33.Location = new System.Drawing.Point(17, 221);
            this.label33.Name = "label33";
            this.label33.Size = new System.Drawing.Size(152, 25);
            this.label33.TabIndex = 165;
            this.label33.Text = "Save file every * mins";
            this.label33.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label33, "How often the avi file AND the other data files are saved");
            //
            // uniquefilecheck
            //
            this.uniquefilecheck.Location = new System.Drawing.Point(44, 245);
            this.uniquefilecheck.Name = "uniquefilecheck";
            this.uniquefilecheck.Size = new System.Drawing.Size(120, 25);
            this.uniquefilecheck.TabIndex = 164;
            this.uniquefilecheck.Text = "unique file name?";
            this.toolTip1.SetToolTip(this.uniquefilecheck, "Whether the data files are given a unqiu file name - ");
            //
            // tracerhydrofile
            //
            this.tracerhydrofile.Location = new System.Drawing.Point(617, 12);
            this.tracerhydrofile.Name = "tracerhydrofile";
            this.tracerhydrofile.Size = new System.Drawing.Size(120, 20);
            this.tracerhydrofile.TabIndex = 105;
            this.tracerhydrofile.Text = "null";
            this.tracerhydrofile.Visible = false;
            //
            // mine_input_textBox
            //
            this.mine_input_textBox.Location = new System.Drawing.Point(430, 133);
            this.mine_input_textBox.Name = "mine_input_textBox";
            this.mine_input_textBox.Size = new System.Drawing.Size(100, 20);
            this.mine_input_textBox.TabIndex = 104;
            this.mine_input_textBox.Text = "null";
            this.mine_input_textBox.Visible = false;
            //
            // bedrockbox
            //
            this.bedrockbox.Location = new System.Drawing.Point(131, 97);
            this.bedrockbox.Name = "bedrockbox";
            this.bedrockbox.Size = new System.Drawing.Size(120, 20);
            this.bedrockbox.TabIndex = 102;
            this.bedrockbox.Text = "null";
            //
            // graindataloadbox
            //
            this.graindataloadbox.Location = new System.Drawing.Point(131, 73);
            this.graindataloadbox.Name = "graindataloadbox";
            this.graindataloadbox.Size = new System.Drawing.Size(120, 20);
            this.graindataloadbox.TabIndex = 101;
            this.graindataloadbox.Text = "null";
            //
            // openfiletextbox
            //
            this.openfiletextbox.Location = new System.Drawing.Point(131, 49);
            this.openfiletextbox.Name = "openfiletextbox";
            this.openfiletextbox.Size = new System.Drawing.Size(120, 20);
            this.openfiletextbox.TabIndex = 100;
            this.openfiletextbox.Text = "whole9.dat";
            //
            // tracerbox
            //
            this.tracerbox.Location = new System.Drawing.Point(845, 11);
            this.tracerbox.Name = "tracerbox";
            this.tracerbox.Size = new System.Drawing.Size(88, 23);
            this.tracerbox.TabIndex = 99;
            this.tracerbox.Text = "tracer run?";
            this.toolTip1.SetToolTip(this.tracerbox, "Check to run in \'tracer mode\'");
            this.tracerbox.Visible = false;
            //
            // label39
            //
            this.label39.Location = new System.Drawing.Point(19, 97);
            this.label39.Name = "label39";
            this.label39.Size = new System.Drawing.Size(104, 24);
            this.label39.TabIndex = 96;
            this.label39.Text = "Bedrock data file";
            this.label39.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label39, "A DEM of the bedrock - below which the model cannot erode");
            //
            // label24
            //
            this.label24.Location = new System.Drawing.Point(19, 73);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(104, 24);
            this.label24.TabIndex = 58;
            this.label24.Text = "Grain data file";
            this.label24.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label23
            //
            this.label23.Location = new System.Drawing.Point(19, 49);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(104, 24);
            this.label23.TabIndex = 56;
            this.label23.Text = "DEM data file";
            this.label23.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // groupBoxOutputDirectory
            //
            this.groupBoxOutputDirectory.Controls.Add(this.labelOutDir);
            this.groupBoxOutputDirectory.Controls.Add(this.textBoxOutDir);
            this.groupBoxOutputDirectory.Controls.Add(this.buttonOutDir);
            this.groupBoxOutputDirectory.Controls.Add(this.checkboxOutDirDateTime);
            this.groupBoxOutputDirectory.Location = new System.Drawing.Point(600, 30);
            this.groupBoxOutputDirectory.Name = "groupBoxOutputDirectory";
            this.groupBoxOutputDirectory.Size = new System.Drawing.Size(418, 150);
            this.groupBoxOutputDirectory.TabIndex = 217;
            this.groupBoxOutputDirectory.TabStop = false;
            this.groupBoxOutputDirectory.Text = "Output directory";
            //
            // labelOutDir
            //
            this.labelOutDir.Location = new System.Drawing.Point(19, 24);
            this.labelOutDir.Name = "labelOutDir";
            this.labelOutDir.Size = new System.Drawing.Size(330, 24);
            this.labelOutDir.TabIndex = 0;
            this.labelOutDir.Text = "Set directory for outputs (defaults to the model run directory):";
            this.labelOutDir.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            //
            // textBoxOutDir
            //
            this.textBoxOutDir.Location = new System.Drawing.Point(19, 55);
            this.textBoxOutDir.Name = "textBoxOutDir";
            this.textBoxOutDir.Size = new System.Drawing.Size(330, 20);
            this.textBoxOutDir.TabIndex = 1;
            this.textBoxOutDir.TextChanged += new System.EventHandler(this.textBoxOutDir_TextChanged);
            //
            // buttonOutDir
            //
            this.buttonOutDir.Location = new System.Drawing.Point(355, 55);
            this.buttonOutDir.Name = "buttonOutDir";
            this.buttonOutDir.Size = new System.Drawing.Size(30, 20);
            this.buttonOutDir.TabIndex = 2;
            this.buttonOutDir.Text = "...";
            this.buttonOutDir.Click += new System.EventHandler(this.buttonOutDir_Click);
            //
            // checkboxOutDirDateTime
            //
            this.checkboxOutDirDateTime.Location = new System.Drawing.Point(19, 90);
            this.checkboxOutDirDateTime.Name = "checkboxOutDirDateTime";
            this.checkboxOutDirDateTime.Size = new System.Drawing.Size(380, 30);
            this.checkboxOutDirDateTime.TabIndex = 3;
            this.checkboxOutDirDateTime.Text = "If checked, outputs will be saved in a subfolder of the above path, using the Dat" +
    "e-Time of the simulation start";
            this.checkboxOutDirDateTime.Click += new System.EventHandler(this.checkboxOutDirDateTime_CheckChanged);
            //
            // NumericalTab
            //
            this.NumericalTab.Controls.Add(this.bedslopebox2);
            this.NumericalTab.Controls.Add(this.max_time_step_Box);
            this.NumericalTab.Controls.Add(this.label76);
            this.NumericalTab.Controls.Add(this.veltaubox);
            this.NumericalTab.Controls.Add(this.label52);
            this.NumericalTab.Controls.Add(this.bedslope_box);
            this.NumericalTab.Controls.Add(this.label47);
            this.NumericalTab.Controls.Add(this.mintimestepbox);
            this.NumericalTab.Controls.Add(this.textBox1);
            this.NumericalTab.Controls.Add(this.smoothbox);
            this.NumericalTab.Controls.Add(this.cyclemaxbox);
            this.NumericalTab.Controls.Add(this.itermaxbox);
            this.NumericalTab.Controls.Add(this.limitbox);
            this.NumericalTab.Controls.Add(this.label31);
            this.NumericalTab.Controls.Add(this.label27);
            this.NumericalTab.Controls.Add(this.label26);
            this.NumericalTab.Controls.Add(this.label10);
            this.NumericalTab.Controls.Add(this.label49);
            this.NumericalTab.Location = new System.Drawing.Point(4, 22);
            this.NumericalTab.Name = "NumericalTab";
            this.NumericalTab.Size = new System.Drawing.Size(1323, 504);
            this.NumericalTab.TabIndex = 2;
            this.NumericalTab.Text = "Numerical";
            this.NumericalTab.UseVisualStyleBackColor = true;
            //
            // bedslopebox2
            //
            this.bedslopebox2.Location = new System.Drawing.Point(84, 298);
            this.bedslopebox2.Name = "bedslopebox2";
            this.bedslopebox2.Size = new System.Drawing.Size(153, 35);
            this.bedslopebox2.TabIndex = 189;
            this.bedslopebox2.Text = "redundant now hidden";
            this.bedslopebox2.Visible = false;
            this.bedslopebox2.CheckedChanged += new System.EventHandler(this.bedslopebox2_CheckedChanged);
            //
            // max_time_step_Box
            //
            this.max_time_step_Box.Location = new System.Drawing.Point(184, 80);
            this.max_time_step_Box.Name = "max_time_step_Box";
            this.max_time_step_Box.Size = new System.Drawing.Size(64, 20);
            this.max_time_step_Box.TabIndex = 186;
            this.max_time_step_Box.Text = "3600";
            //
            // label76
            //
            this.label76.Location = new System.Drawing.Point(40, 80);
            this.label76.Name = "label76";
            this.label76.Size = new System.Drawing.Size(136, 24);
            this.label76.TabIndex = 187;
            this.label76.Text = "Max time step (secs)";
            this.label76.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // veltaubox
            //
            this.veltaubox.Location = new System.Drawing.Point(77, 368);
            this.veltaubox.Name = "veltaubox";
            this.veltaubox.Size = new System.Drawing.Size(160, 27);
            this.veltaubox.TabIndex = 183;
            this.veltaubox.Text = "Tau based on velocity";
            this.veltaubox.Visible = false;
            this.veltaubox.CheckedChanged += new System.EventHandler(this.veltaubox_CheckedChanged);
            //
            // label52
            //
            this.label52.Location = new System.Drawing.Point(81, 342);
            this.label52.Name = "label52";
            this.label52.Size = new System.Drawing.Size(133, 28);
            this.label52.TabIndex = 182;
            this.label52.Text = "Slope used to calc Tau (for erosion)";
            this.toolTip1.SetToolTip(this.label52, "See notes on lateral tab for more. ");
            this.label52.Visible = false;
            //
            // bedslope_box
            //
            this.bedslope_box.Checked = true;
            this.bedslope_box.CheckState = System.Windows.Forms.CheckState.Checked;
            this.bedslope_box.Location = new System.Drawing.Point(22, 345);
            this.bedslope_box.Name = "bedslope_box";
            this.bedslope_box.Size = new System.Drawing.Size(192, 35);
            this.bedslope_box.TabIndex = 180;
            this.bedslope_box.Text = "Bedslope (original method)";
            this.bedslope_box.Visible = false;
            this.bedslope_box.CheckedChanged += new System.EventHandler(this.bedslope_box_CheckedChanged);
            //
            // label47
            //
            this.label47.Location = new System.Drawing.Point(256, 53);
            this.label47.Name = "label47";
            this.label47.Size = new System.Drawing.Size(66, 23);
            this.label47.TabIndex = 176;
            this.label47.Text = "(caution !)";
            this.label47.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // mintimestepbox
            //
            this.mintimestepbox.Location = new System.Drawing.Point(184, 53);
            this.mintimestepbox.Name = "mintimestepbox";
            this.mintimestepbox.Size = new System.Drawing.Size(64, 20);
            this.mintimestepbox.TabIndex = 166;
            this.mintimestepbox.Text = "1";
            //
            // textBox1
            //
            this.textBox1.Location = new System.Drawing.Point(184, 118);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(64, 20);
            this.textBox1.TabIndex = 164;
            this.textBox1.Text = "0";
            //
            // smoothbox
            //
            this.smoothbox.Location = new System.Drawing.Point(163, 360);
            this.smoothbox.Name = "smoothbox";
            this.smoothbox.Size = new System.Drawing.Size(40, 20);
            this.smoothbox.TabIndex = 160;
            this.smoothbox.Text = "1";
            this.smoothbox.Visible = false;
            //
            // cyclemaxbox
            //
            this.cyclemaxbox.Location = new System.Drawing.Point(184, 142);
            this.cyclemaxbox.Name = "cyclemaxbox";
            this.cyclemaxbox.Size = new System.Drawing.Size(64, 20);
            this.cyclemaxbox.TabIndex = 155;
            this.cyclemaxbox.Text = "1000";
            //
            // itermaxbox
            //
            this.itermaxbox.Location = new System.Drawing.Point(200, 318);
            this.itermaxbox.Name = "itermaxbox";
            this.itermaxbox.Size = new System.Drawing.Size(64, 20);
            this.itermaxbox.TabIndex = 154;
            this.itermaxbox.Text = "100000";
            this.itermaxbox.Visible = false;
            //
            // limitbox
            //
            this.limitbox.Location = new System.Drawing.Point(184, 179);
            this.limitbox.Name = "limitbox";
            this.limitbox.Size = new System.Drawing.Size(40, 20);
            this.limitbox.TabIndex = 144;
            this.limitbox.Text = "1";
            //
            // label31
            //
            this.label31.Location = new System.Drawing.Point(56, 118);
            this.label31.Name = "label31";
            this.label31.Size = new System.Drawing.Size(120, 24);
            this.label31.TabIndex = 165;
            this.label31.Text = "run start time (h)";
            this.label31.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label27
            //
            this.label27.Location = new System.Drawing.Point(64, 142);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(112, 24);
            this.label27.TabIndex = 157;
            this.label27.Text = "max run duration (h)";
            this.label27.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label26
            //
            this.label26.Location = new System.Drawing.Point(56, 318);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(136, 24);
            this.label26.TabIndex = 156;
            this.label26.Text = "max # of iterations";
            this.label26.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label26.Visible = false;
            //
            // label10
            //
            this.label10.Location = new System.Drawing.Point(56, 175);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(120, 24);
            this.label10.TabIndex = 152;
            this.label10.Text = "memory limit";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label49
            //
            this.label49.Location = new System.Drawing.Point(40, 53);
            this.label49.Name = "label49";
            this.label49.Size = new System.Drawing.Size(136, 24);
            this.label49.TabIndex = 167;
            this.label49.Text = "Min time step (s)";
            this.label49.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // GrainTab
            //
            this.GrainTab.Controls.Add(this.label125);
            this.GrainTab.Controls.Add(this.landslide_grainsize);
            this.GrainTab.Controls.Add(this.label120);
            this.GrainTab.Controls.Add(this.label119);
            this.GrainTab.Controls.Add(this.label111);
            this.GrainTab.Controls.Add(this.label112);
            this.GrainTab.Controls.Add(this.label113);
            this.GrainTab.Controls.Add(this.label114);
            this.GrainTab.Controls.Add(this.label115);
            this.GrainTab.Controls.Add(this.label116);
            this.GrainTab.Controls.Add(this.label117);
            this.GrainTab.Controls.Add(this.label118);
            this.GrainTab.Controls.Add(this.gp9_box);
            this.GrainTab.Controls.Add(this.gp8_box);
            this.GrainTab.Controls.Add(this.gp7_box);
            this.GrainTab.Controls.Add(this.gp6_box);
            this.GrainTab.Controls.Add(this.gp5_box);
            this.GrainTab.Controls.Add(this.gp4_box);
            this.GrainTab.Controls.Add(this.gp3_box);
            this.GrainTab.Controls.Add(this.gp2_box);
            this.GrainTab.Controls.Add(this.gp1_box);
            this.GrainTab.Controls.Add(this.g9_box);
            this.GrainTab.Controls.Add(this.g8_box);
            this.GrainTab.Controls.Add(this.g7_box);
            this.GrainTab.Controls.Add(this.g6_box);
            this.GrainTab.Controls.Add(this.g5_box);
            this.GrainTab.Controls.Add(this.g4_box);
            this.GrainTab.Controls.Add(this.g3_box);
            this.GrainTab.Controls.Add(this.g2_box);
            this.GrainTab.Controls.Add(this.g1_box);
            this.GrainTab.Controls.Add(this.label32);
            this.GrainTab.Controls.Add(this.label30);
            this.GrainTab.Controls.Add(this.meyerbox);
            this.GrainTab.Controls.Add(this.checkBox8);
            this.GrainTab.Controls.Add(this.bedrock_erosion_threshold_box);
            this.GrainTab.Controls.Add(this.bedrock_erosion_rate_box);
            this.GrainTab.Controls.Add(this.label92);
            this.GrainTab.Controls.Add(this.label93);
            this.GrainTab.Controls.Add(this.label65);
            this.GrainTab.Controls.Add(this.textBox7);
            this.GrainTab.Controls.Add(this.label58);
            this.GrainTab.Controls.Add(this.downstreamshiftbox);
            this.GrainTab.Controls.Add(this.label55);
            this.GrainTab.Controls.Add(this.max_vel_box);
            this.GrainTab.Controls.Add(this.textBox3);
            this.GrainTab.Controls.Add(this.label60);
            this.GrainTab.Controls.Add(this.avge_smoothbox);
            this.GrainTab.Controls.Add(this.nolateral);
            this.GrainTab.Controls.Add(this.newlateral);
            this.GrainTab.Controls.Add(this.label7);
            this.GrainTab.Controls.Add(this.lateralratebox);
            this.GrainTab.Controls.Add(this.label48);
            this.GrainTab.Controls.Add(this.label54);
            this.GrainTab.Controls.Add(this.propremaining);
            this.GrainTab.Controls.Add(this.label50);
            this.GrainTab.Controls.Add(this.activebox);
            this.GrainTab.Controls.Add(this.erodefactorbox);
            this.GrainTab.Controls.Add(this.label12);
            this.GrainTab.Controls.Add(this.einsteinbox);
            this.GrainTab.Controls.Add(this.wilcockbox);
            this.GrainTab.Controls.Add(this.fallGS9box);
            this.GrainTab.Controls.Add(this.fallGS8box);
            this.GrainTab.Controls.Add(this.fallGS7box);
            this.GrainTab.Controls.Add(this.fallGS6box);
            this.GrainTab.Controls.Add(this.fallGS5box);
            this.GrainTab.Controls.Add(this.fallGS4box);
            this.GrainTab.Controls.Add(this.fallGS3box);
            this.GrainTab.Controls.Add(this.gpSumLabel);
            this.GrainTab.Controls.Add(this.gpSumLabel2);
            this.GrainTab.Controls.Add(this.suspGS9box);
            this.GrainTab.Controls.Add(this.suspGS8box);
            this.GrainTab.Controls.Add(this.suspGS7box);
            this.GrainTab.Controls.Add(this.suspGS6box);
            this.GrainTab.Controls.Add(this.suspGS5box);
            this.GrainTab.Controls.Add(this.suspGS4box);
            this.GrainTab.Controls.Add(this.suspGS3box);
            this.GrainTab.Controls.Add(this.suspGS2box);
            this.GrainTab.Controls.Add(this.fallGS2box);
            this.GrainTab.Controls.Add(this.fallGS1box);
            this.GrainTab.Controls.Add(this.label28);
            this.GrainTab.Controls.Add(this.label4);
            this.GrainTab.Controls.Add(this.suspGS1box);
            this.GrainTab.Controls.Add(this.gp3box);
            this.GrainTab.Controls.Add(this.gp4box);
            this.GrainTab.Controls.Add(this.gp5box);
            this.GrainTab.Controls.Add(this.gp6box);
            this.GrainTab.Controls.Add(this.gp7box);
            this.GrainTab.Controls.Add(this.gp8box);
            this.GrainTab.Controls.Add(this.gp9box);
            this.GrainTab.Controls.Add(this.gp2box);
            this.GrainTab.Controls.Add(this.gp1box);
            this.GrainTab.Controls.Add(this.g3box);
            this.GrainTab.Controls.Add(this.g4box);
            this.GrainTab.Controls.Add(this.g5box);
            this.GrainTab.Controls.Add(this.g6box);
            this.GrainTab.Controls.Add(this.g7box);
            this.GrainTab.Controls.Add(this.g8box);
            this.GrainTab.Controls.Add(this.g9box);
            this.GrainTab.Controls.Add(this.g2box);
            this.GrainTab.Controls.Add(this.g1box);
            this.GrainTab.Controls.Add(this.label22);
            this.GrainTab.Controls.Add(this.label21);
            this.GrainTab.Controls.Add(this.label20);
            this.GrainTab.Controls.Add(this.label19);
            this.GrainTab.Controls.Add(this.label18);
            this.GrainTab.Controls.Add(this.label17);
            this.GrainTab.Controls.Add(this.label16);
            this.GrainTab.Controls.Add(this.label15);
            this.GrainTab.Controls.Add(this.label14);
            this.GrainTab.Controls.Add(this.label13);
            this.GrainTab.Controls.Add(this.label6);
            this.GrainTab.Location = new System.Drawing.Point(4, 22);
            this.GrainTab.Name = "GrainTab";
            this.GrainTab.Size = new System.Drawing.Size(1323, 504);
            this.GrainTab.TabIndex = 3;
            this.GrainTab.Text = "Sediment";
            this.GrainTab.UseVisualStyleBackColor = true;
            //
            // label125
            //
            this.label125.Location = new System.Drawing.Point(828, 62);
            this.label125.Name = "label125";
            this.label125.Size = new System.Drawing.Size(80, 39);
            this.label125.TabIndex = 252;
            this.label125.Text = "size2";
            this.label125.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label125.Visible = false;
            this.label125.Click += new System.EventHandler(this.label125_Click);
            //
            // landslide_grainsize
            //
            this.landslide_grainsize.AutoSize = true;
            this.landslide_grainsize.Location = new System.Drawing.Point(782, 25);
            this.landslide_grainsize.Name = "landslide_grainsize";
            this.landslide_grainsize.Size = new System.Drawing.Size(114, 17);
            this.landslide_grainsize.TabIndex = 251;
            this.landslide_grainsize.Text = "landslide_grainsize";
            this.landslide_grainsize.UseVisualStyleBackColor = true;
            this.landslide_grainsize.CheckedChanged += new System.EventHandler(this.landslide_grainsize_CheckedChanged);
            //
            // label120
            //
            this.label120.ForeColor = System.Drawing.SystemColors.ControlText;
            this.label120.Location = new System.Drawing.Point(875, 292);
            this.label120.Name = "label120";
            this.label120.Size = new System.Drawing.Size(117, 28);
            this.label120.TabIndex = 250;
            this.label120.Text = "sum must equal 1.0";
            this.label120.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.label120.Visible = false;
            //
            // label119
            //
            this.label119.AutoSize = true;
            this.label119.Location = new System.Drawing.Point(39, 30);
            this.label119.Name = "label119";
            this.label119.Size = new System.Drawing.Size(62, 13);
            this.label119.TabIndex = 249;
            this.label119.Text = "particle one";
            //
            // label111
            //
            this.label111.Location = new System.Drawing.Point(828, 90);
            this.label111.Name = "label111";
            this.label111.Size = new System.Drawing.Size(80, 39);
            this.label111.TabIndex = 247;
            this.label111.Text = "size3";
            this.label111.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label111.Visible = false;
            //
            // label112
            //
            this.label112.Location = new System.Drawing.Point(828, 120);
            this.label112.Name = "label112";
            this.label112.Size = new System.Drawing.Size(80, 38);
            this.label112.TabIndex = 246;
            this.label112.Text = "size4";
            this.label112.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label112.Visible = false;
            //
            // label113
            //
            this.label113.Location = new System.Drawing.Point(828, 146);
            this.label113.Name = "label113";
            this.label113.Size = new System.Drawing.Size(80, 38);
            this.label113.TabIndex = 245;
            this.label113.Text = "size5";
            this.label113.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label113.Visible = false;
            //
            // label114
            //
            this.label114.Location = new System.Drawing.Point(828, 171);
            this.label114.Name = "label114";
            this.label114.Size = new System.Drawing.Size(80, 40);
            this.label114.TabIndex = 244;
            this.label114.Text = "size6";
            this.label114.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label114.Visible = false;
            //
            // label115
            //
            this.label115.Location = new System.Drawing.Point(828, 197);
            this.label115.Name = "label115";
            this.label115.Size = new System.Drawing.Size(80, 39);
            this.label115.TabIndex = 243;
            this.label115.Text = "size7";
            this.label115.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label115.Visible = false;
            //
            // label116
            //
            this.label116.Location = new System.Drawing.Point(828, 226);
            this.label116.Name = "label116";
            this.label116.Size = new System.Drawing.Size(80, 39);
            this.label116.TabIndex = 242;
            this.label116.Text = "size8";
            this.label116.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label116.Visible = false;
            //
            // label117
            //
            this.label117.Location = new System.Drawing.Point(828, 251);
            this.label117.Name = "label117";
            this.label117.Size = new System.Drawing.Size(80, 40);
            this.label117.TabIndex = 241;
            this.label117.Text = "size9";
            this.label117.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label117.Visible = false;
            //
            // label118
            //
            this.label118.Location = new System.Drawing.Point(827, 38);
            this.label118.Name = "label118";
            this.label118.Size = new System.Drawing.Size(80, 40);
            this.label118.TabIndex = 240;
            this.label118.Text = "size1";
            this.label118.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label118.Visible = false;
            //
            // gp9_box
            //
            this.gp9_box.Location = new System.Drawing.Point(997, 262);
            this.gp9_box.Name = "gp9_box";
            this.gp9_box.Size = new System.Drawing.Size(130, 20);
            this.gp9_box.TabIndex = 239;
            this.gp9_box.Text = "0.121";
            this.gp9_box.Visible = false;
            //
            // gp8_box
            //
            this.gp8_box.Location = new System.Drawing.Point(997, 236);
            this.gp8_box.Name = "gp8_box";
            this.gp8_box.Name = "gp8_box";
            this.gp8_box.Size = new System.Drawing.Size(130, 20);
            this.gp8_box.TabIndex = 238;
            this.gp8_box.Text = "0.231";
            this.gp8_box.Visible = false;
            //
            // gp7_box
            //
            this.gp7_box.Location = new System.Drawing.Point(997, 207);
            this.gp7_box.Name = "gp7_box";
            this.gp7_box.Size = new System.Drawing.Size(130, 20);
            this.gp7_box.TabIndex = 237;
            this.gp7_box.Text = "0.220";
            this.gp7_box.Visible = false;
            //
            // gp6_box
            //
            this.gp6_box.Location = new System.Drawing.Point(997, 182);
            this.gp6_box.Name = "gp6_box";
            this.gp6_box.Size = new System.Drawing.Size(130, 20);
            this.gp6_box.TabIndex = 236;
            this.gp6_box.Text = "0.146";
            this.gp6_box.Visible = false;
            //
            // gp5_box
            //
            this.gp5_box.Location = new System.Drawing.Point(997, 156);
            this.gp5_box.Name = "gp5_box";
            this.gp5_box.Size = new System.Drawing.Size(130, 20);
            this.gp5_box.TabIndex = 235;
            this.gp5_box.Text = "0.068";
            this.gp5_box.Visible = false;
            //
            // gp4_box
            //
            this.gp4_box.Location = new System.Drawing.Point(997, 130);
            this.gp4_box.Name = "gp4_box";
            this.gp4_box.Size = new System.Drawing.Size(130, 20);
            this.gp4_box.TabIndex = 234;
            this.gp4_box.Text = "0.029";
            this.gp4_box.Visible = false;
            //
            // gp3_box
            //
            this.gp3_box.Location = new System.Drawing.Point(997, 104);
            this.gp3_box.Name = "gp3_box";
            this.gp3_box.Size = new System.Drawing.Size(130, 20);
            this.gp3_box.TabIndex = 233;
            this.gp3_box.Text = "0.019";
            this.gp3_box.Visible = false;
            //
            // gp2_box
            //
            this.gp2_box.Location = new System.Drawing.Point(997, 75);
            this.gp2_box.Name = "gp2_box";
            this.gp2_box.Size = new System.Drawing.Size(130, 20);
            this.gp2_box.TabIndex = 232;
            this.gp2_box.Text = "0.022";
            this.gp2_box.Visible = false;
            //
            // gp1_box
            //
            this.gp1_box.Location = new System.Drawing.Point(997, 49);
            this.gp1_box.Name = "gp1_box";
            this.gp1_box.Size = new System.Drawing.Size(130, 20);
            this.gp1_box.TabIndex = 231;
            this.gp1_box.Text = "0.144";
            this.gp1_box.Visible = false;
            //
            // g9_box
            //
            this.g9_box.Location = new System.Drawing.Point(911, 262);
            this.g9_box.Name = "g9_box";
            this.g9_box.Size = new System.Drawing.Size(71, 20);
            this.g9_box.TabIndex = 230;
            this.g9_box.Text = "0.128";
            this.g9_box.Visible = false;
            //
            // g8_box
            //
            this.g8_box.Location = new System.Drawing.Point(911, 236);
            this.g8_box.Name = "g8_box";
            this.g8_box.Size = new System.Drawing.Size(71, 20);
            this.g8_box.TabIndex = 229;
            this.g8_box.Text = "0.064";
            this.g8_box.Visible = false;
            //
            // g7_box
            //
            this.g7_box.Location = new System.Drawing.Point(911, 208);
            this.g7_box.Name = "g7_box";
            this.g7_box.Size = new System.Drawing.Size(71, 20);
            this.g7_box.TabIndex = 228;
            this.g7_box.Text = "0.032";
            this.g7_box.Visible = false;
            //
            // g6_box
            //
            this.g6_box.Location = new System.Drawing.Point(911, 182);
            this.g6_box.Name = "g6_box";
            this.g6_box.Size = new System.Drawing.Size(71, 20);
            this.g6_box.TabIndex = 227;
            this.g6_box.Text = "0.016";
            this.g6_box.Visible = false;
            //
            // g5_box
            //
            this.g5_box.Location = new System.Drawing.Point(911, 156);
            this.g5_box.Name = "g5_box";
            this.g5_box.Size = new System.Drawing.Size(71, 20);
            this.g5_box.TabIndex = 226;
            this.g5_box.Text = "0.008";
            this.g5_box.Visible = false;
            //
            // g4_box
            //
            this.g4_box.Location = new System.Drawing.Point(911, 130);
            this.g4_box.Name = "g4_box";
            this.g4_box.Size = new System.Drawing.Size(71, 20);
            this.g4_box.TabIndex = 225;
            this.g4_box.Text = "0.004";
            this.g4_box.Visible = false;
            //
            // g3_box
            //
            this.g3_box.Location = new System.Drawing.Point(911, 102);
            this.g3_box.Name = "g3_box";
            this.g3_box.Size = new System.Drawing.Size(71, 20);
            this.g3_box.TabIndex = 224;
            this.g3_box.Text = "0.002";
            this.g3_box.Visible = false;
            //
            // g2_box
            //
            this.g2_box.Location = new System.Drawing.Point(911, 76);
            this.g2_box.Name = "g2_box";
            this.g2_box.Size = new System.Drawing.Size(71, 20);
            this.g2_box.TabIndex = 223;
            this.g2_box.Text = "0.001";
            this.g2_box.Visible = false;
            //
            // g1_box
            //
            this.g1_box.Location = new System.Drawing.Point(911, 49);
            this.g1_box.Name = "g1_box";
            this.g1_box.Size = new System.Drawing.Size(71, 20);
            this.g1_box.TabIndex = 222;
            this.g1_box.Text = "0.0005";
            this.g1_box.Visible = false;
            //
            // label32
            //
            this.label32.AutoSize = true;
            this.label32.Location = new System.Drawing.Point(994, 26);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(54, 13);
            this.label32.TabIndex = 220;
            this.label32.Text = "proportion";
            this.label32.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label32.Visible = false;
            //
            // label30
            //
            this.label30.AutoSize = true;
            this.label30.Location = new System.Drawing.Point(911, 26);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(68, 13);
            this.label30.TabIndex = 219;
            this.label30.Text = "grain size (m)";
            this.label30.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.label30.Visible = false;
            //
            // meyerbox
            //
            this.meyerbox.AutoSize = true;
            this.meyerbox.Location = new System.Drawing.Point(549, 71);
            this.meyerbox.Name = "meyerbox";
            this.meyerbox.Size = new System.Drawing.Size(114, 17);
            this.meyerbox.TabIndex = 107;
            this.meyerbox.Text = "Meyer Peter Muller";
            this.meyerbox.UseVisualStyleBackColor = true;
            this.meyerbox.CheckedChanged += new System.EventHandler(this.meyerbox_CheckedChanged);
            //
            // checkBox8
            //
            this.checkBox8.Checked = true;
            this.checkBox8.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox8.Location = new System.Drawing.Point(27, 322);
            this.checkBox8.Name = "checkBox8";
            this.checkBox8.Size = new System.Drawing.Size(169, 28);
            this.checkBox8.TabIndex = 218;
            this.checkBox8.Text = "All 9 grainsizes?";
            //
            // bedrock_erosion_threshold_box
            //
            this.bedrock_erosion_threshold_box.Location = new System.Drawing.Point(27, 362);
            this.bedrock_erosion_threshold_box.Name = "bedrock_erosion_threshold_box";
            this.bedrock_erosion_threshold_box.Size = new System.Drawing.Size(100, 20);
            this.bedrock_erosion_threshold_box.TabIndex = 214;
            this.bedrock_erosion_threshold_box.Text = "0";
            //
            // bedrock_erosion_rate_box
            //
            this.bedrock_erosion_rate_box.Location = new System.Drawing.Point(27, 395);
            this.bedrock_erosion_rate_box.Name = "bedrock_erosion_rate_box";
            this.bedrock_erosion_rate_box.Size = new System.Drawing.Size(100, 20);
            this.bedrock_erosion_rate_box.TabIndex = 215;
            this.bedrock_erosion_rate_box.Text = "0";
            //
            // label92
            //
            this.label92.Location = new System.Drawing.Point(133, 358);
            this.label92.Name = "label92";
            this.label92.Size = new System.Drawing.Size(167, 33);
            this.label92.TabIndex = 217;
            this.label92.Text = "Bedrock erosion threshold (Pa)";
            this.label92.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label93
            //
            this.label93.Location = new System.Drawing.Point(138, 393);
            this.label93.Name = "label93";
            this.label93.Size = new System.Drawing.Size(162, 24);
            this.label93.TabIndex = 216;
            this.label93.Text = "Bedrock erosion rate (m/Pa/Yr)";
            this.label93.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label65
            //
            this.label65.Location = new System.Drawing.Point(630, 415);
            this.label65.Name = "label65";
            this.label65.Size = new System.Drawing.Size(131, 27);
            this.label65.TabIndex = 213;
            this.label65.Text = "Max difference allowed in cross channel smoothing";
            //
            // textBox7
            //
            this.textBox7.Location = new System.Drawing.Point(549, 415);
            this.textBox7.Name = "textBox7";
            this.textBox7.Size = new System.Drawing.Size(73, 20);
            this.textBox7.TabIndex = 212;
            this.textBox7.Text = "0.0001";
            //
            // label58
            //
            this.label58.Location = new System.Drawing.Point(630, 384);
            this.label58.Name = "label58";
            this.label58.Size = new System.Drawing.Size(131, 27);
            this.label58.TabIndex = 211;
            this.label58.Text = "Number of cells to shift lat erosion downstream";
            //
            // downstreamshiftbox
            //
            this.downstreamshiftbox.Location = new System.Drawing.Point(549, 384);
            this.downstreamshiftbox.Name = "downstreamshiftbox";
            this.downstreamshiftbox.Size = new System.Drawing.Size(73, 20);
            this.downstreamshiftbox.TabIndex = 210;
            this.downstreamshiftbox.Text = "5";
            //
            // label55
            //
            this.label55.Location = new System.Drawing.Point(627, 103);
            this.label55.Name = "label55";
            this.label55.Size = new System.Drawing.Size(195, 27);
            this.label55.TabIndex = 209;
            this.label55.Text = "Max velocity used to calc Tau from vel.";
            //
            // max_vel_box
            //
            this.max_vel_box.Location = new System.Drawing.Point(547, 104);
            this.max_vel_box.Name = "max_vel_box";
            this.max_vel_box.Size = new System.Drawing.Size(40, 20);
            this.max_vel_box.TabIndex = 208;
            this.max_vel_box.Text = "5";
            //
            // textBox3
            //
            this.textBox3.Location = new System.Drawing.Point(548, 324);
            this.textBox3.Name = "textBox3";
            this.textBox3.Size = new System.Drawing.Size(40, 20);
            this.textBox3.TabIndex = 202;
            this.textBox3.Text = "0";
            //
            // label60
            //
            this.label60.Location = new System.Drawing.Point(627, 350);
            this.label60.Name = "label60";
            this.label60.Size = new System.Drawing.Size(131, 27);
            this.label60.TabIndex = 201;
            this.label60.Text = "Number of passes for edge smoothing filter";
            //
            // avge_smoothbox
            //
            this.avge_smoothbox.Location = new System.Drawing.Point(548, 353);
            this.avge_smoothbox.Name = "avge_smoothbox";
            this.avge_smoothbox.Size = new System.Drawing.Size(73, 20);
            this.avge_smoothbox.TabIndex = 200;
            this.avge_smoothbox.Text = "100";
            //
            // nolateral
            //
            this.nolateral.Checked = true;
            this.nolateral.CheckState = System.Windows.Forms.CheckState.Checked;
            this.nolateral.Location = new System.Drawing.Point(386, 334);
            this.nolateral.Name = "nolateral";
            this.nolateral.Size = new System.Drawing.Size(121, 28);
            this.nolateral.TabIndex = 199;
            this.nolateral.Text = "No Lateral erosion";
            this.nolateral.Visible = false;
            //
            // newlateral
            //
            this.newlateral.Location = new System.Drawing.Point(550, 290);
            this.newlateral.Name = "newlateral";
            this.newlateral.Size = new System.Drawing.Size(106, 28);
            this.newlateral.TabIndex = 198;
            this.newlateral.Text = "Lateral Erosion";
            //
            // label7
            //
            this.label7.Location = new System.Drawing.Point(615, 323);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(95, 24);
            this.label7.TabIndex = 197;
            this.label7.Text = "Lat erosion rate";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label7, "Lateral erosion constant that is applied to method 1 (old laterla) and method 2 (" +
        "new lateral)");
            //
            // lateralratebox
            //
            this.lateralratebox.Location = new System.Drawing.Point(547, 256);
            this.lateralratebox.Name = "lateralratebox";
            this.lateralratebox.Size = new System.Drawing.Size(64, 20);
            this.lateralratebox.TabIndex = 196;
            this.lateralratebox.Text = "20";
            //
            // label48
            //
            this.label48.Location = new System.Drawing.Point(627, 253);
            this.label48.Name = "label48";
            this.label48.Size = new System.Drawing.Size(152, 24);
            this.label48.TabIndex = 195;
            this.label48.Text = "in channel lateral erosion rate";
            this.label48.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // label54
            //
            this.label54.Location = new System.Drawing.Point(627, 214);
            this.label54.Name = "label54";
            this.label54.Size = new System.Drawing.Size(195, 27);
            this.label54.TabIndex = 193;
            this.label54.Text = "Proportion re-circulated if recirculate box is checked";
            //
            // propremaining
            //
            this.propremaining.Location = new System.Drawing.Point(547, 217);
            this.propremaining.Name = "propremaining";
            this.propremaining.Size = new System.Drawing.Size(64, 20);
            this.propremaining.TabIndex = 192;
            this.propremaining.Text = "1.0";
            //
            // label50
            //
            this.label50.Location = new System.Drawing.Point(622, 154);
            this.label50.Name = "label50";
            this.label50.Size = new System.Drawing.Size(172, 48);
            this.label50.TabIndex = 173;
            this.label50.Text = "Active layer thickness (m) must be at least 4 times max erode limit";
            this.label50.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // activebox
            //
            this.activebox.Location = new System.Drawing.Point(547, 160);
            this.activebox.Name = "activebox";
            this.activebox.Size = new System.Drawing.Size(64, 20);
            this.activebox.TabIndex = 172;
            this.activebox.Text = "0.1";
            //
            // erodefactorbox
            //
            this.erodefactorbox.Location = new System.Drawing.Point(547, 133);
            this.erodefactorbox.Name = "erodefactorbox";
            this.erodefactorbox.Size = new System.Drawing.Size(40, 20);
            this.erodefactorbox.TabIndex = 170;
            this.erodefactorbox.Text = "0.02";
            //
            // label12
            //
            this.label12.Location = new System.Drawing.Point(617, 130);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(87, 24);
            this.label12.TabIndex = 171;
            this.label12.Text = "Max erode limit";
            this.label12.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // einsteinbox
            //
            this.einsteinbox.AutoSize = true;
            this.einsteinbox.Location = new System.Drawing.Point(549, 51);
            this.einsteinbox.Name = "einsteinbox";
            this.einsteinbox.Size = new System.Drawing.Size(63, 17);
            this.einsteinbox.TabIndex = 107;
            this.einsteinbox.Text = "Einstein";
            this.einsteinbox.UseVisualStyleBackColor = true;
            this.einsteinbox.CheckedChanged += new System.EventHandler(this.einsteinbox_CheckedChanged);
            //
            // wilcockbox
            //
            this.wilcockbox.AutoSize = true;
            this.wilcockbox.Checked = true;
            this.wilcockbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.wilcockbox.Location = new System.Drawing.Point(548, 32);
            this.wilcockbox.Name = "wilcockbox";
            this.wilcockbox.Size = new System.Drawing.Size(119, 17);
            this.wilcockbox.TabIndex = 106;
            this.wilcockbox.Text = "Wilcock and Crowe";
            this.wilcockbox.UseVisualStyleBackColor = true;
            this.wilcockbox.CheckedChanged += new System.EventHandler(this.wilcockbox_CheckedChanged);
            //
            // fallGS9box
            //
            this.fallGS9box.Enabled = false;
            this.fallGS9box.Location = new System.Drawing.Point(416, 378);
            this.fallGS9box.Name = "fallGS9box";
            this.fallGS9box.Size = new System.Drawing.Size(100, 20);
            this.fallGS9box.TabIndex = 90;
            this.fallGS9box.Text = "1.357";
            this.fallGS9box.Visible = false;
            //
            // fallGS8box
            //
            this.fallGS8box.Enabled = false;
            this.fallGS8box.Location = new System.Drawing.Point(376, 378);
            this.fallGS8box.Name = "fallGS8box";
            this.fallGS8box.Size = new System.Drawing.Size(100, 20);
            this.fallGS8box.TabIndex = 89;
            this.fallGS8box.Text = "0.959";
            this.fallGS8box.Visible = false;
            //
            // fallGS7box
            //
            this.fallGS7box.Enabled = false;
            this.fallGS7box.Location = new System.Drawing.Point(424, 343);
            this.fallGS7box.Name = "fallGS7box";
            this.fallGS7box.Size = new System.Drawing.Size(100, 20);
            this.fallGS7box.TabIndex = 88;
            this.fallGS7box.Text = "0.678";
            this.fallGS7box.Visible = false;
            //
            // fallGS6box
            //
            this.fallGS6box.Enabled = false;
            this.fallGS6box.Location = new System.Drawing.Point(386, 364);
            this.fallGS6box.Name = "fallGS6box";
            this.fallGS6box.Size = new System.Drawing.Size(100, 20);
            this.fallGS6box.TabIndex = 87;
            this.fallGS6box.Text = "0.479";
            this.fallGS6box.Visible = false;
            //
            // fallGS5box
            //
            this.fallGS5box.Enabled = false;
            this.fallGS5box.Location = new System.Drawing.Point(386, 364);
            this.fallGS5box.Name = "fallGS5box";
            this.fallGS5box.Size = new System.Drawing.Size(100, 20);
            this.fallGS5box.TabIndex = 86;
            this.fallGS5box.Text = "0.338";
            this.fallGS5box.Visible = false;
            //
            // fallGS4box
            //
            this.fallGS4box.Enabled = false;
            this.fallGS4box.Location = new System.Drawing.Point(393, 368);
            this.fallGS4box.Name = "fallGS4box";
            this.fallGS4box.Size = new System.Drawing.Size(100, 20);
            this.fallGS4box.TabIndex = 85;
            this.fallGS4box.Text = "0.237";
            this.fallGS4box.Visible = false;
            //
            // fallGS3box
            //
            this.fallGS3box.Enabled = false;
            this.fallGS3box.Location = new System.Drawing.Point(386, 364);
            this.fallGS3box.Name = "fallGS3box";
            this.fallGS3box.Size = new System.Drawing.Size(100, 20);
            this.fallGS3box.TabIndex = 84;
            this.fallGS3box.Text = "0.164";
            this.fallGS3box.Visible = false;
            //
            // gpSumLabel
            //
            this.gpSumLabel.Location = new System.Drawing.Point(229, 343);
            this.gpSumLabel.Name = "gpSumLabel";
            this.gpSumLabel.Size = new System.Drawing.Size(96, 16);
            this.gpSumLabel.TabIndex = 105;
            this.gpSumLabel.Text = "OK";
            this.gpSumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // gpSumLabel2
            //
            this.gpSumLabel2.ForeColor = System.Drawing.SystemColors.ControlText;
            this.gpSumLabel2.Location = new System.Drawing.Point(229, 319);
            this.gpSumLabel2.Name = "gpSumLabel2";
            this.gpSumLabel2.Size = new System.Drawing.Size(112, 16);
            this.gpSumLabel2.TabIndex = 104;
            this.gpSumLabel2.Text = "sum must equal 1.0";
            this.gpSumLabel2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // suspGS9box
            //
            this.suspGS9box.Location = new System.Drawing.Point(401, 374);
            this.suspGS9box.Name = "suspGS9box";
            this.suspGS9box.Size = new System.Drawing.Size(16, 24);
            this.suspGS9box.TabIndex = 81;
            this.suspGS9box.Visible = false;
            this.suspGS9box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS8box
            //
            this.suspGS8box.Location = new System.Drawing.Point(394, 368);
            this.suspGS8box.Name = "suspGS8box";
            this.suspGS8box.Size = new System.Drawing.Size(16, 24);
            this.suspGS8box.TabIndex = 80;
            this.suspGS8box.Visible = false;
            this.suspGS8box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS7box
            //
            this.suspGS7box.Location = new System.Drawing.Point(394, 368);
            this.suspGS7box.Name = "suspGS7box";
            this.suspGS7box.Size = new System.Drawing.Size(16, 24);
            this.suspGS7box.TabIndex = 79;
            this.suspGS7box.Visible = false;
            this.suspGS7box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS6box
            //
            this.suspGS6box.Location = new System.Drawing.Point(401, 372);
            this.suspGS6box.Name = "suspGS6box";
            this.suspGS6box.Size = new System.Drawing.Size(16, 24);
            this.suspGS6box.TabIndex = 78;
            this.suspGS6box.Visible = false;
            this.suspGS6box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS5box
            //
            this.suspGS5box.Location = new System.Drawing.Point(416, 372);
            this.suspGS5box.Name = "suspGS5box";
            this.suspGS5box.Size = new System.Drawing.Size(16, 24);
            this.suspGS5box.TabIndex = 77;
            this.suspGS5box.Visible = false;
            this.suspGS5box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS4box
            //
            this.suspGS4box.Location = new System.Drawing.Point(399, 368);
            this.suspGS4box.Name = "suspGS4box";
            this.suspGS4box.Size = new System.Drawing.Size(16, 24);
            this.suspGS4box.TabIndex = 76;
            this.suspGS4box.Visible = false;
            this.suspGS4box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS3box
            //
            this.suspGS3box.Location = new System.Drawing.Point(423, 368);
            this.suspGS3box.Name = "suspGS3box";
            this.suspGS3box.Size = new System.Drawing.Size(16, 24);
            this.suspGS3box.TabIndex = 75;
            this.suspGS3box.Visible = false;
            this.suspGS3box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // suspGS2box
            //
            this.suspGS2box.Location = new System.Drawing.Point(416, 371);
            this.suspGS2box.Name = "suspGS2box";
            this.suspGS2box.Size = new System.Drawing.Size(16, 24);
            this.suspGS2box.TabIndex = 74;
            this.suspGS2box.Visible = false;
            this.suspGS2box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // fallGS2box
            //
            this.fallGS2box.Location = new System.Drawing.Point(386, 364);
            this.fallGS2box.Name = "fallGS2box";
            this.fallGS2box.Size = new System.Drawing.Size(100, 20);
            this.fallGS2box.TabIndex = 83;
            this.fallGS2box.Text = "0.109";
            this.fallGS2box.Visible = false;
            //
            // fallGS1box
            //
            this.fallGS1box.Location = new System.Drawing.Point(424, 40);
            this.fallGS1box.Name = "fallGS1box";
            this.fallGS1box.Size = new System.Drawing.Size(100, 20);
            this.fallGS1box.TabIndex = 82;
            this.fallGS1box.Text = "0.066";
            //
            // label28
            //
            this.label28.Location = new System.Drawing.Point(432, 16);
            this.label28.Name = "label28";
            this.label28.Size = new System.Drawing.Size(88, 16);
            this.label28.TabIndex = 95;
            this.label28.Text = "fall velocity (m/s)";
            this.label28.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label4
            //
            this.label4.Location = new System.Drawing.Point(336, 16);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(96, 16);
            this.label4.TabIndex = 85;
            this.label4.Text = "suspended ?";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // suspGS1box
            //
            this.suspGS1box.Location = new System.Drawing.Point(376, 40);
            this.suspGS1box.Name = "suspGS1box";
            this.suspGS1box.Size = new System.Drawing.Size(16, 24);
            this.suspGS1box.TabIndex = 73;
            this.suspGS1box.CheckedChanged += new System.EventHandler(this.suspCheckedChange);
            //
            // gp3box
            //
            this.gp3box.Location = new System.Drawing.Point(232, 104);
            this.gp3box.Name = "gp3box";
            this.gp3box.Size = new System.Drawing.Size(100, 20);
            this.gp3box.TabIndex = 66;
            this.gp3box.Text = "0.019";
            this.gp3box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp4box
            //
            this.gp4box.Location = new System.Drawing.Point(232, 136);
            this.gp4box.Name = "gp4box";
            this.gp4box.Size = new System.Drawing.Size(100, 20);
            this.gp4box.TabIndex = 67;
            this.gp4box.Text = "0.029";
            this.gp4box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp5box
            //
            this.gp5box.Location = new System.Drawing.Point(232, 168);
            this.gp5box.Name = "gp5box";
            this.gp5box.Size = new System.Drawing.Size(100, 20);
            this.gp5box.TabIndex = 68;
            this.gp5box.Text = "0.068";
            this.gp5box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp6box
            //
            this.gp6box.Location = new System.Drawing.Point(232, 200);
            this.gp6box.Name = "gp6box";
            this.gp6box.Size = new System.Drawing.Size(100, 20);
            this.gp6box.TabIndex = 69;
            this.gp6box.Text = "0.146";
            this.gp6box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp7box
            //
            this.gp7box.Location = new System.Drawing.Point(232, 232);
            this.gp7box.Name = "gp7box";
            this.gp7box.Size = new System.Drawing.Size(100, 20);
            this.gp7box.TabIndex = 70;
            this.gp7box.Text = "0.220";
            this.gp7box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp8box
            //
            this.gp8box.Location = new System.Drawing.Point(232, 264);
            this.gp8box.Name = "gp8box";
            this.gp8box.Size = new System.Drawing.Size(100, 20);
            this.gp8box.TabIndex = 71;
            this.gp8box.Text = "0.231";
            this.gp8box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp9box
            //
            this.gp9box.Location = new System.Drawing.Point(232, 296);
            this.gp9box.Name = "gp9box";
            this.gp9box.Size = new System.Drawing.Size(100, 20);
            this.gp9box.TabIndex = 72;
            this.gp9box.Text = "0.121";
            this.gp9box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp2box
            //
            this.gp2box.Location = new System.Drawing.Point(232, 72);
            this.gp2box.Name = "gp2box";
            this.gp2box.Size = new System.Drawing.Size(100, 20);
            this.gp2box.TabIndex = 65;
            this.gp2box.Text = "0.022";
            this.gp2box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // gp1box
            //
            this.gp1box.Location = new System.Drawing.Point(232, 40);
            this.gp1box.Name = "gp1box";
            this.gp1box.Size = new System.Drawing.Size(100, 20);
            this.gp1box.TabIndex = 64;
            this.gp1box.Text = "0.144";
            this.gp1box.TextChanged += new System.EventHandler(this.fracGSchanged);
            //
            // g3box
            //
            this.g3box.Location = new System.Drawing.Point(96, 104);
            this.g3box.Name = "g3box";
            this.g3box.Size = new System.Drawing.Size(100, 20);
            this.g3box.TabIndex = 57;
            this.g3box.Text = "0.002";
            //
            // g4box
            //
            this.g4box.Location = new System.Drawing.Point(96, 136);
            this.g4box.Name = "g4box";
            this.g4box.Size = new System.Drawing.Size(100, 20);
            this.g4box.TabIndex = 58;
            this.g4box.Text = "0.004";
            //
            // g5box
            //
            this.g5box.Location = new System.Drawing.Point(96, 168);
            this.g5box.Name = "g5box";
            this.g5box.Size = new System.Drawing.Size(100, 20);
            this.g5box.TabIndex = 59;
            this.g5box.Text = "0.008";
            //
            // g6box
            //
            this.g6box.Location = new System.Drawing.Point(96, 200);
            this.g6box.Name = "g6box";
            this.g6box.Size = new System.Drawing.Size(100, 20);
            this.g6box.TabIndex = 60;
            this.g6box.Text = "0.016";
            //
            // g7box
            //
            this.g7box.Location = new System.Drawing.Point(96, 232);
            this.g7box.Name = "g7box";
            this.g7box.Size = new System.Drawing.Size(100, 20);
            this.g7box.TabIndex = 61;
            this.g7box.Text = "0.032";
            //
            // g8box
            //
            this.g8box.Location = new System.Drawing.Point(96, 264);
            this.g8box.Name = "g8box";
            this.g8box.Size = new System.Drawing.Size(100, 20);
            this.g8box.TabIndex = 62;
            this.g8box.Text = "0.064";
            //
            // g9box
            //
            this.g9box.Location = new System.Drawing.Point(96, 296);
            this.g9box.Name = "g9box";
            this.g9box.Size = new System.Drawing.Size(100, 20);
            this.g9box.TabIndex = 63;
            this.g9box.Text = "0.128";
            //
            // g2box
            //
            this.g2box.Location = new System.Drawing.Point(96, 72);
            this.g2box.Name = "g2box";
            this.g2box.Size = new System.Drawing.Size(100, 20);
            this.g2box.TabIndex = 56;
            this.g2box.Text = "0.001";
            //
            // g1box
            //
            this.g1box.Location = new System.Drawing.Point(96, 40);
            this.g1box.Name = "g1box";
            this.g1box.Size = new System.Drawing.Size(100, 20);
            this.g1box.TabIndex = 55;
            this.g1box.Text = "0.0005";
            //
            // label22
            //
            this.label22.Location = new System.Drawing.Point(232, 16);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(96, 16);
            this.label22.TabIndex = 83;
            this.label22.Text = "proportion";
            this.label22.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label21
            //
            this.label21.Location = new System.Drawing.Point(96, 16);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(96, 16);
            this.label21.TabIndex = 82;
            this.label21.Text = "grain size (m)";
            this.label21.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label20
            //
            this.label20.Location = new System.Drawing.Point(24, 72);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(40, 24);
            this.label20.TabIndex = 81;
            this.label20.Text = "size2";
            this.label20.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label19
            //
            this.label19.Location = new System.Drawing.Point(24, 104);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(40, 24);
            this.label19.TabIndex = 80;
            this.label19.Text = "size3";
            this.label19.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label18
            //
            this.label18.Location = new System.Drawing.Point(24, 136);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(40, 24);
            this.label18.TabIndex = 79;
            this.label18.Text = "size4";
            this.label18.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label17
            //
            this.label17.Location = new System.Drawing.Point(24, 168);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(40, 24);
            this.label17.TabIndex = 78;
            this.label17.Text = "size5";
            this.label17.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label16
            //
            this.label16.Location = new System.Drawing.Point(24, 200);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(40, 24);
            this.label16.TabIndex = 77;
            this.label16.Text = "size6";
            this.label16.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label15
            //
            this.label15.Location = new System.Drawing.Point(24, 232);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(40, 24);
            this.label15.TabIndex = 76;
            this.label15.Text = "size7";
            this.label15.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label14
            //
            this.label14.Location = new System.Drawing.Point(24, 264);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(40, 24);
            this.label14.TabIndex = 75;
            this.label14.Text = "size8";
            this.label14.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label13
            //
            this.label13.Location = new System.Drawing.Point(24, 296);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(40, 24);
            this.label13.TabIndex = 74;
            this.label13.Text = "size9";
            this.label13.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label6
            //
            this.label6.Location = new System.Drawing.Point(24, 40);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(40, 24);
            this.label6.TabIndex = 73;
            this.label6.Text = "size1";
            this.label6.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // DescriptionTab
            //
            this.DescriptionTab.Controls.Add(this.DescBox);
            this.DescriptionTab.Location = new System.Drawing.Point(4, 22);
            this.DescriptionTab.Name = "DescriptionTab";
            this.DescriptionTab.Size = new System.Drawing.Size(1323, 504);
            this.DescriptionTab.TabIndex = 5;
            this.DescriptionTab.Text = "Description";
            this.DescriptionTab.UseVisualStyleBackColor = true;
            //
            // DescBox
            // 
            this.DescBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.DescBox.Location = new System.Drawing.Point(16, 16);
            this.DescBox.Multiline = true;
            this.DescBox.Name = "DescBox";
            this.DescBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.DescBox.Size = new System.Drawing.Size(1126, 681);
            this.DescBox.TabIndex = 132;
            //
            // GridTab
            //
            this.GridTab.Controls.Add(this.overrideheaderBox);
            this.GridTab.Controls.Add(this.dxbox);
            this.GridTab.Controls.Add(this.label11);
            this.GridTab.Controls.Add(this.ytextbox);
            this.GridTab.Controls.Add(this.xtextbox);
            this.GridTab.Controls.Add(this.label2);
            this.GridTab.Controls.Add(this.label1);
            this.GridTab.Location = new System.Drawing.Point(4, 22);
            this.GridTab.Name = "GridTab";
            this.GridTab.Size = new System.Drawing.Size(1323, 504);
            this.GridTab.TabIndex = 1;
            this.GridTab.Text = "Grid";
            this.GridTab.UseVisualStyleBackColor = true;
            //
            // overrideheaderBox
            //
            this.overrideheaderBox.Location = new System.Drawing.Point(16, 16);
            this.overrideheaderBox.Name = "overrideheaderBox";
            this.overrideheaderBox.Size = new System.Drawing.Size(200, 24);
            this.overrideheaderBox.TabIndex = 27;
            this.overrideheaderBox.Text = "override header file";
            this.overrideheaderBox.CheckedChanged += new System.EventHandler(this.overrideheaderBox_CheckedChanged);
            //
            // dxbox
            //
            this.dxbox.Enabled = false;
            this.dxbox.Location = new System.Drawing.Point(120, 88);
            this.dxbox.Name = "dxbox";
            this.dxbox.Size = new System.Drawing.Size(40, 20);
            this.dxbox.TabIndex = 25;
            this.dxbox.Text = "5";
            //
            // label11
            //
            this.label11.Enabled = false;
            this.label11.Location = new System.Drawing.Point(16, 88);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(96, 24);
            this.label11.TabIndex = 26;
            this.label11.Text = "Cell size";
            this.label11.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // ytextbox
            //
            this.ytextbox.Enabled = false;
            this.ytextbox.Location = new System.Drawing.Point(120, 64);
            this.ytextbox.Name = "ytextbox";
            this.ytextbox.Size = new System.Drawing.Size(56, 20);
            this.ytextbox.TabIndex = 5;
            this.ytextbox.Text = "358";
            //
            // xtextbox
            //
            this.xtextbox.Enabled = false;
            this.xtextbox.Location = new System.Drawing.Point(120, 40);
            this.xtextbox.Name = "xtextbox";
            this.xtextbox.Size = new System.Drawing.Size(56, 20);
            this.xtextbox.TabIndex = 4;
            this.xtextbox.Text = "593";
            //
            // label2
            //
            this.label2.Enabled = false;
            this.label2.Location = new System.Drawing.Point(16, 64);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 24);
            this.label2.TabIndex = 7;
            this.label2.Text = "Y coordinates";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label1
            //
            this.label1.Enabled = false;
            this.label1.Location = new System.Drawing.Point(24, 40);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(88, 24);
            this.label1.TabIndex = 6;
            this.label1.Text = "X coordinates";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // HydrologyTab
            //
            this.HydrologyTab.Controls.Add(this.groupBox7);
            this.HydrologyTab.Controls.Add(this.groupBox5);
            this.HydrologyTab.Controls.Add(this.groupBoxWaterSourceTracer);
            this.HydrologyTab.Controls.Add(this.groupBox1);
            this.HydrologyTab.Location = new System.Drawing.Point(4, 22);
            this.HydrologyTab.Name = "HydrologyTab";
            this.HydrologyTab.Size = new System.Drawing.Size(1323, 504);
            this.HydrologyTab.TabIndex = 4;
            this.HydrologyTab.Text = "Hydrology";
            this.HydrologyTab.UseVisualStyleBackColor = true;
            this.HydrologyTab.Click += new System.EventHandler(this.HydrologyTab_Click);
            //
            // groupBox7
            //
            this.groupBox7.Controls.Add(this.label105);
            this.groupBox7.Controls.Add(this.mfiletimestepbox);
            this.groupBox7.Controls.Add(this.hydroindexBox);
            this.groupBox7.Controls.Add(this.label103);
            this.groupBox7.Controls.Add(this.label37);
            this.groupBox7.Controls.Add(this.mvaluebox);
            this.groupBox7.Controls.Add(this.label102);
            this.groupBox7.Controls.Add(this.rfnumBox);
            this.groupBox7.Controls.Add(this.checkBox7);
            this.groupBox7.Controls.Add(this.label35);
            this.groupBox7.Controls.Add(this.raintimestepbox);
            this.groupBox7.Controls.Add(this.jmeaninputfilebox);
            this.groupBox7.Controls.Add(this.label59);
            this.groupBox7.Controls.Add(this.mvalueloadbox);
            this.groupBox7.Controls.Add(this.raindataloadbox);
            this.groupBox7.Controls.Add(this.label25);
            this.groupBox7.Location = new System.Drawing.Point(526, 17);
            this.groupBox7.Name = "groupBox7";
            this.groupBox7.Size = new System.Drawing.Size(427, 281);
            this.groupBox7.TabIndex = 222;
            this.groupBox7.TabStop = false;
            this.groupBox7.Text = "Rainfall input variables";
            //
            // label105
            //
            this.label105.Location = new System.Drawing.Point(106, 144);
            this.label105.Name = "label105";
            this.label105.Size = new System.Drawing.Size(104, 39);
            this.label105.TabIndex = 235;
            this.label105.Text = "Time varying M file time step (min)";
            this.label105.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // mfiletimestepbox
            //
            this.mfiletimestepbox.Location = new System.Drawing.Point(215, 149);
            this.mfiletimestepbox.Name = "mfiletimestepbox";
            this.mfiletimestepbox.Size = new System.Drawing.Size(56, 20);
            this.mfiletimestepbox.TabIndex = 234;
            this.mfiletimestepbox.Text = "1440";
            //
            // hydroindexBox
            //
            this.hydroindexBox.Enabled = false;
            this.hydroindexBox.Location = new System.Drawing.Point(292, 252);
            this.hydroindexBox.Name = "hydroindexBox";
            this.hydroindexBox.Size = new System.Drawing.Size(118, 20);
            this.hydroindexBox.TabIndex = 233;
            this.hydroindexBox.Text = "null";
            //
            // label103
            //
            this.label103.AutoSize = true;
            this.label103.Enabled = false;
            this.label103.Location = new System.Drawing.Point(213, 255);
            this.label103.Name = "label103";
            this.label103.Size = new System.Drawing.Size(74, 13);
            this.label103.TabIndex = 232;
            this.label103.Text = "hydroindex file";
            //
            // label37
            //
            this.label37.Location = new System.Drawing.Point(16, 81);
            this.label37.Name = "label37";
            this.label37.Size = new System.Drawing.Size(128, 24);
            this.label37.TabIndex = 200;
            this.label37.Text = "\'m\' value";
            this.label37.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // mvaluebox
            //
            this.mvaluebox.Location = new System.Drawing.Point(153, 85);
            this.mvaluebox.Name = "mvaluebox";
            this.mvaluebox.Size = new System.Drawing.Size(40, 20);
            this.mvaluebox.TabIndex = 199;
            this.mvaluebox.Text = "0.01";
            //
            // label102
            //
            this.label102.AutoSize = true;
            this.label102.Enabled = false;
            this.label102.Location = new System.Drawing.Point(40, 255);
            this.label102.Name = "label102";
            this.label102.Size = new System.Drawing.Size(98, 13);
            this.label102.TabIndex = 231;
            this.label102.Text = "number of rain cells";
            //
            // rfnumBox
            //
            this.rfnumBox.Enabled = false;
            this.rfnumBox.Location = new System.Drawing.Point(143, 252);
            this.rfnumBox.Name = "rfnumBox";
            this.rfnumBox.Size = new System.Drawing.Size(56, 20);
            this.rfnumBox.TabIndex = 230;
            this.rfnumBox.Text = "1";
            //
            // checkBox7
            //
            this.checkBox7.AutoSize = true;
            this.checkBox7.Location = new System.Drawing.Point(23, 227);
            this.checkBox7.Name = "checkBox7";
            this.checkBox7.Size = new System.Drawing.Size(297, 17);
            this.checkBox7.TabIndex = 229;
            this.checkBox7.Text = "Spatially variable rainfall and M value (if M value file used)";
            this.checkBox7.UseVisualStyleBackColor = true;
            this.checkBox7.CheckedChanged += new System.EventHandler(this.checkBox7_CheckedChanged);
            //
            // label35
            //
            this.label35.Location = new System.Drawing.Point(40, 47);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(104, 39);
            this.label35.TabIndex = 228;
            this.label35.Text = "Rainfall data file time step (min)";
            this.label35.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // raintimestepbox
            //
            this.raintimestepbox.Location = new System.Drawing.Point(153, 51);
            this.raintimestepbox.Name = "raintimestepbox";
            this.raintimestepbox.Size = new System.Drawing.Size(56, 20);
            this.raintimestepbox.TabIndex = 227;
            this.raintimestepbox.Text = "60";
            //
            // jmeaninputfilebox
            //
            this.jmeaninputfilebox.AutoSize = true;
            this.jmeaninputfilebox.Location = new System.Drawing.Point(23, 201);
            this.jmeaninputfilebox.Name = "jmeaninputfilebox";
            this.jmeaninputfilebox.Size = new System.Drawing.Size(263, 17);
            this.jmeaninputfilebox.TabIndex = 226;
            this.jmeaninputfilebox.Text = "if checked, discharge is read direct from rainfall file";
            this.jmeaninputfilebox.UseVisualStyleBackColor = true;
            //
            // label59
            //
            this.label59.AutoSize = true;
            this.label59.Location = new System.Drawing.Point(40, 126);
            this.label59.Name = "label59";
            this.label59.Size = new System.Drawing.Size(95, 13);
            this.label59.TabIndex = 225;
            this.label59.Text = "Time varying M file";
            //
            // mvalueloadbox
            //
            this.mvalueloadbox.Location = new System.Drawing.Point(153, 120);
            this.mvalueloadbox.Name = "mvalueloadbox";
            this.mvalueloadbox.Size = new System.Drawing.Size(118, 20);
            this.mvalueloadbox.TabIndex = 224;
            this.mvalueloadbox.Text = "null";
            //
            // raindataloadbox
            //
            this.raindataloadbox.Location = new System.Drawing.Point(152, 25);
            this.raindataloadbox.Name = "raindataloadbox";
            this.raindataloadbox.Size = new System.Drawing.Size(120, 20);
            this.raindataloadbox.TabIndex = 223;
            this.raindataloadbox.Text = "null";
            //
            // label25
            //
            this.label25.Location = new System.Drawing.Point(40, 25);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(104, 24);
            this.label25.TabIndex = 222;
            this.label25.Text = "Rainfall data file";
            this.label25.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label25, "Hourly rainfall data - in an ascii format");
            //
            // groupBox5
            //
            this.groupBox5.Controls.Add(this.label83);
            this.groupBox5.Controls.Add(this.div_inputs_box);
            this.groupBox5.Controls.Add(this.inboxExtra);
            this.groupBox5.Controls.Add(this.infileExtra);
            this.groupBox5.Controls.Add(this.infile8);
            this.groupBox5.Controls.Add(this.ybox8);
            this.groupBox5.Controls.Add(this.xbox8);
            this.groupBox5.Controls.Add(this.inbox8);
            this.groupBox5.Controls.Add(this.infile7);
            this.groupBox5.Controls.Add(this.ybox7);
            this.groupBox5.Controls.Add(this.xbox7);
            this.groupBox5.Controls.Add(this.inbox7);
            this.groupBox5.Controls.Add(this.infile6);
            this.groupBox5.Controls.Add(this.ybox6);
            this.groupBox5.Controls.Add(this.xbox6);
            this.groupBox5.Controls.Add(this.inbox6);
            this.groupBox5.Controls.Add(this.infile5);
            this.groupBox5.Controls.Add(this.ybox5);
            this.groupBox5.Controls.Add(this.xbox5);
            this.groupBox5.Controls.Add(this.inbox5);
            this.groupBox5.Controls.Add(this.input_time_step_box);
            this.groupBox5.Controls.Add(this.infile4);
            this.groupBox5.Controls.Add(this.infile3);
            this.groupBox5.Controls.Add(this.infile2);
            this.groupBox5.Controls.Add(this.infile1);
            this.groupBox5.Controls.Add(this.ybox1);
            this.groupBox5.Controls.Add(this.ybox2);
            this.groupBox5.Controls.Add(this.ybox3);
            this.groupBox5.Controls.Add(this.ybox4);
            this.groupBox5.Controls.Add(this.xbox2);
            this.groupBox5.Controls.Add(this.xbox3);
            this.groupBox5.Controls.Add(this.xbox4);
            this.groupBox5.Controls.Add(this.xbox1);
            this.groupBox5.Controls.Add(this.label29);
            this.groupBox5.Controls.Add(this.label44);
            this.groupBox5.Controls.Add(this.label43);
            this.groupBox5.Controls.Add(this.label41);
            this.groupBox5.Controls.Add(this.inbox2);
            this.groupBox5.Controls.Add(this.inbox3);
            this.groupBox5.Controls.Add(this.inbox4);
            this.groupBox5.Controls.Add(this.inbox1);
            this.groupBox5.Controls.Add(this.label42);
            this.groupBox5.Location = new System.Drawing.Point(32, 17);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(463, 332);
            this.groupBox5.TabIndex = 214;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Reach input variables";
            //
            // label83
            //
            this.label83.Location = new System.Drawing.Point(118, 300);
            this.label83.Name = "label83";
            this.label83.Size = new System.Drawing.Size(100, 20);
            this.label83.TabIndex = 202;
            this.label83.Text = "Divide inputs by..";
            //
            // div_inputs_box
            //
            this.div_inputs_box.Location = new System.Drawing.Point(224, 297);
            this.div_inputs_box.Name = "div_inputs_box";
            this.div_inputs_box.Size = new System.Drawing.Size(31, 20);
            this.div_inputs_box.TabIndex = 201;
            this.div_inputs_box.Text = "1";
            //
            // inboxExtra
            //
            this.inboxExtra.Location = new System.Drawing.Point(9, 264);
            this.inboxExtra.Name = "inboxExtra";
            this.inboxExtra.Size = new System.Drawing.Size(156, 16);
            this.inboxExtra.TabIndex = 203;
            this.inboxExtra.Text = "Load extra inputs from file:";
            //
            // infileExtra
            //
            this.infileExtra.Location = new System.Drawing.Point(166, 264);
            this.infileExtra.Name = "infileExtra";
            this.infileExtra.Size = new System.Drawing.Size(120, 20);
            this.infileExtra.TabIndex = 204;
            //
            // infile8
            //
            this.infile8.Location = new System.Drawing.Point(166, 236);
            this.infile8.Name = "infile8";
            this.infile8.Size = new System.Drawing.Size(120, 20);
            this.infile8.TabIndex = 198;
            //
            // ybox8
            //
            this.ybox8.Location = new System.Drawing.Point(120, 236);
            this.ybox8.Name = "ybox8";
            this.ybox8.Size = new System.Drawing.Size(31, 20);
            this.ybox8.TabIndex = 197;
            //
            // xbox8
            //
            this.xbox8.Location = new System.Drawing.Point(73, 236);
            this.xbox8.Name = "xbox8";
            this.xbox8.Size = new System.Drawing.Size(32, 20);
            this.xbox8.TabIndex = 196;
            //
            // inbox8
            //
            this.inbox8.Location = new System.Drawing.Point(9, 236);
            this.inbox8.Name = "inbox8";
            this.inbox8.Size = new System.Drawing.Size(64, 16);
            this.inbox8.TabIndex = 195;
            this.inbox8.Text = "Input 8";
            //
            // infile7
            //
            this.infile7.Location = new System.Drawing.Point(166, 208);
            this.infile7.Name = "infile7";
            this.infile7.Size = new System.Drawing.Size(120, 20);
            this.infile7.TabIndex = 194;
            //
            // ybox7
            //
            this.ybox7.Location = new System.Drawing.Point(120, 208);
            this.ybox7.Name = "ybox7";
            this.ybox7.Size = new System.Drawing.Size(31, 20);
            this.ybox7.TabIndex = 193;
            //
            // xbox7
            //
            this.xbox7.Location = new System.Drawing.Point(73, 208);
            this.xbox7.Name = "xbox7";
            this.xbox7.Size = new System.Drawing.Size(32, 20);
            this.xbox7.TabIndex = 192;
            //
            // inbox7
            //
            this.inbox7.Location = new System.Drawing.Point(9, 208);
            this.inbox7.Name = "inbox7";
            this.inbox7.Size = new System.Drawing.Size(64, 17);
            this.inbox7.TabIndex = 191;
            this.inbox7.Text = "Input 7";
            //
            // infile6
            //
            this.infile6.Location = new System.Drawing.Point(166, 180);
            this.infile6.Name = "infile6";
            this.infile6.Size = new System.Drawing.Size(120, 20);
            this.infile6.TabIndex = 190;
            //
            // ybox6
            //
            this.ybox6.Location = new System.Drawing.Point(120, 180);
            this.ybox6.Name = "ybox6";
            this.ybox6.Size = new System.Drawing.Size(31, 20);
            this.ybox6.TabIndex = 189;
            //
            // xbox6
            //
            this.xbox6.Location = new System.Drawing.Point(73, 180);
            this.xbox6.Name = "xbox6";
            this.xbox6.Size = new System.Drawing.Size(32, 20);
            this.xbox6.TabIndex = 188;
            //
            // inbox6
            //
            this.inbox6.Location = new System.Drawing.Point(9, 180);
            this.inbox6.Name = "inbox6";
            this.inbox6.Size = new System.Drawing.Size(64, 17);
            this.inbox6.TabIndex = 187;
            this.inbox6.Text = "Input 6";
            //
            // infile5
            //
            this.infile5.Location = new System.Drawing.Point(166, 153);
            this.infile5.Name = "infile5";
            this.infile5.Size = new System.Drawing.Size(120, 20);
            this.infile5.TabIndex = 186;
            //
            // ybox5
            //
            this.ybox5.Location = new System.Drawing.Point(120, 153);
            this.ybox5.Name = "ybox5";
            this.ybox5.Size = new System.Drawing.Size(31, 20);
            this.ybox5.TabIndex = 185;
            //
            // xbox5
            //
            this.xbox5.Location = new System.Drawing.Point(73, 153);
            this.xbox5.Name = "xbox5";
            this.xbox5.Size = new System.Drawing.Size(32, 20);
            this.xbox5.TabIndex = 184;
            //
            // inbox5
            //
            this.inbox5.Location = new System.Drawing.Point(9, 153);
            this.inbox5.Name = "inbox5";
            this.inbox5.Size = new System.Drawing.Size(64, 16);
            this.inbox5.TabIndex = 183;
            this.inbox5.Text = "Input 5";
            //
            // input_time_step_box
            //
            this.input_time_step_box.Location = new System.Drawing.Point(320, 90);
            this.input_time_step_box.Name = "input_time_step_box";
            this.input_time_step_box.Size = new System.Drawing.Size(63, 20);
            this.input_time_step_box.TabIndex = 181;
            this.input_time_step_box.Text = "1440";
            //
            // infile4
            //
            this.infile4.Location = new System.Drawing.Point(166, 127);
            this.infile4.Name = "infile4";
            this.infile4.Size = new System.Drawing.Size(120, 20);
            this.infile4.TabIndex = 180;
            //
            // infile3
            //
            this.infile3.Location = new System.Drawing.Point(166, 103);
            this.infile3.Name = "infile3";
            this.infile3.Size = new System.Drawing.Size(120, 20);
            this.infile3.TabIndex = 179;
            //
            // infile2
            //
            this.infile2.Location = new System.Drawing.Point(166, 79);
            this.infile2.Name = "infile2";
            this.infile2.Size = new System.Drawing.Size(120, 20);
            this.infile2.TabIndex = 178;
            //
            // infile1
            //
            this.infile1.Location = new System.Drawing.Point(166, 55);
            this.infile1.Name = "infile1";
            this.infile1.Size = new System.Drawing.Size(120, 20);
            this.infile1.TabIndex = 177;
            //
            // ybox1
            //
            this.ybox1.Location = new System.Drawing.Point(121, 55);
            this.ybox1.Name = "ybox1";
            this.ybox1.Size = new System.Drawing.Size(32, 20);
            this.ybox1.TabIndex = 175;
            //
            // ybox2
            //
            this.ybox2.Location = new System.Drawing.Point(121, 79);
            this.ybox2.Name = "ybox2";
            this.ybox2.Size = new System.Drawing.Size(32, 20);
            this.ybox2.TabIndex = 174;
            //
            // ybox3
            //
            this.ybox3.Location = new System.Drawing.Point(121, 103);
            this.ybox3.Name = "ybox3";
            this.ybox3.Size = new System.Drawing.Size(32, 20);
            this.ybox3.TabIndex = 173;
            //
            // ybox4
            //
            this.ybox4.Location = new System.Drawing.Point(121, 127);
            this.ybox4.Name = "ybox4";
            this.ybox4.Size = new System.Drawing.Size(32, 20);
            this.ybox4.TabIndex = 172;
            //
            // xbox2
            //
            this.xbox2.Location = new System.Drawing.Point(73, 79);
            this.xbox2.Name = "xbox2";
            this.xbox2.Size = new System.Drawing.Size(32, 20);
            this.xbox2.TabIndex = 171;
            //
            // xbox3
            //
            this.xbox3.Location = new System.Drawing.Point(73, 103);
            this.xbox3.Name = "xbox3";
            this.xbox3.Size = new System.Drawing.Size(32, 20);
            this.xbox3.TabIndex = 170;
            //
            // xbox4
            //
            this.xbox4.Location = new System.Drawing.Point(73, 127);
            this.xbox4.Name = "xbox4";
            this.xbox4.Size = new System.Drawing.Size(32, 20);
            this.xbox4.TabIndex = 169;
            //
            // xbox1
            //
            this.xbox1.Location = new System.Drawing.Point(73, 55);
            this.xbox1.Name = "xbox1";
            this.xbox1.Size = new System.Drawing.Size(32, 20);
            this.xbox1.TabIndex = 168;
            //
            // label29
            //
            this.label29.Location = new System.Drawing.Point(317, 60);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(100, 32);
            this.label29.TabIndex = 182;
            this.label29.Text = "input data time step (mins)";
            //
            // label44
            //
            this.label44.Location = new System.Drawing.Point(169, 31);
            this.label44.Name = "label44";
            this.label44.Size = new System.Drawing.Size(56, 16);
            this.label44.TabIndex = 176;
            this.label44.Text = "File Name";
            //
            // label43
            //
            this.label43.Location = new System.Drawing.Point(129, 31);
            this.label43.Name = "label43";
            this.label43.Size = new System.Drawing.Size(16, 16);
            this.label43.TabIndex = 167;
            this.label43.Text = "Y";
            //
            // label41
            //
            this.label41.Location = new System.Drawing.Point(81, 31);
            this.label41.Name = "label41";
            this.label41.Size = new System.Drawing.Size(16, 16);
            this.label41.TabIndex = 165;
            this.label41.Text = "X";
            //
            // inbox2
            //
            this.inbox2.Location = new System.Drawing.Point(9, 79);
            this.inbox2.Name = "inbox2";
            this.inbox2.Size = new System.Drawing.Size(64, 16);
            this.inbox2.TabIndex = 164;
            this.inbox2.Text = "Input 2";
            //
            // inbox3
            //
            this.inbox3.Location = new System.Drawing.Point(9, 103);
            this.inbox3.Name = "inbox3";
            this.inbox3.Size = new System.Drawing.Size(64, 16);
            this.inbox3.TabIndex = 163;
            this.inbox3.Text = "Input 3";
            //
            // inbox4
            //
            this.inbox4.Location = new System.Drawing.Point(9, 127);
            this.inbox4.Name = "inbox4";
            this.inbox4.Size = new System.Drawing.Size(64, 16);
            this.inbox4.TabIndex = 162;
            this.inbox4.Text = "Input 4";
            //
            // inbox1
            //
            this.inbox1.Location = new System.Drawing.Point(9, 55);
            this.inbox1.Name = "inbox1";
            this.inbox1.Size = new System.Drawing.Size(64, 16);
            this.inbox1.TabIndex = 161;
            this.inbox1.Text = "Input 1";
            //
            // label42
            //
            this.label42.Location = new System.Drawing.Point(81, 31);
            this.label42.Name = "label42";
            this.label42.Size = new System.Drawing.Size(16, 16);
            this.label42.TabIndex = 166;
            this.label42.Text = "X";
            //
            // groupBoxWaterSourceTracer
            //
            this.groupBoxWaterSourceTracer.Controls.Add(this.checkBoxSoluteTracer);
            this.groupBoxWaterSourceTracer.Controls.Add(this.textBox20);
            this.groupBoxWaterSourceTracer.Controls.Add(this.checkBox11);
            this.groupBoxWaterSourceTracer.Controls.Add(this.checkBox10);
            this.groupBoxWaterSourceTracer.Location = new System.Drawing.Point(32, 360);
            this.groupBoxWaterSourceTracer.Name = "groupBoxWaterSourceTracer";
            this.groupBoxWaterSourceTracer.Size = new System.Drawing.Size(463, 90);
            this.groupBoxWaterSourceTracer.TabIndex = 223;
            this.groupBoxWaterSourceTracer.TabStop = false;
            this.groupBoxWaterSourceTracer.Text = "Water source and solute tracing";
            //
            // checkBoxSoluteTracer
            //
            this.checkBoxSoluteTracer.AutoSize = true;
            this.checkBoxSoluteTracer.Enabled = false;
            this.checkBoxSoluteTracer.Location = new System.Drawing.Point(150, 54);
            this.checkBoxSoluteTracer.Name = "checkBoxSoluteTracer";
            this.checkBoxSoluteTracer.Size = new System.Drawing.Size(90, 17);
            this.checkBoxSoluteTracer.TabIndex = 227;
            this.checkBoxSoluteTracer.Text = "Trace solutes";
            this.toolTip1.SetToolTip(this.checkBoxSoluteTracer, "Activate tracing of time variable solutes input for each source");
            this.checkBoxSoluteTracer.UseVisualStyleBackColor = true;
            this.checkBoxSoluteTracer.CheckedChanged += new System.EventHandler(this.checkBoxSoluteTracer_CheckedChanged);
            //
            // textBox20
            //
            this.textBox20.Enabled = false;
            this.textBox20.Location = new System.Drawing.Point(320, 24);
            this.textBox20.Name = "textBox20";
            this.textBox20.Size = new System.Drawing.Size(118, 20);
            this.textBox20.TabIndex = 228;
            this.textBox20.Text = "null";
            //
            // checkBox11
            //
            this.checkBox11.AutoSize = true;
            this.checkBox11.Enabled = false;
            this.checkBox11.Location = new System.Drawing.Point(150, 24);
            this.checkBox11.Name = "checkBox11";
            this.checkBox11.Size = new System.Drawing.Size(147, 17);
            this.checkBox11.TabIndex = 227;
            this.checkBox11.Text = "Use rainfall zonation map:";
            this.toolTip1.SetToolTip(this.checkBox11, "Activate water source tracing for surface water (model runs slower)");
            this.checkBox11.UseVisualStyleBackColor = true;
            this.checkBox11.CheckedChanged += new System.EventHandler(this.checkBox11_CheckedChanged);
            //
            // checkBox10
            //
            this.checkBox10.AutoSize = true;
            this.checkBox10.Location = new System.Drawing.Point(9, 24);
            this.checkBox10.Name = "checkBox10";
            this.checkBox10.Size = new System.Drawing.Size(123, 17);
            this.checkBox10.TabIndex = 226;
            this.checkBox10.Text = "Trace water sources";
            this.toolTip1.SetToolTip(this.checkBox10, "Activate water source tracing for surface water (model runs slower)");
            this.checkBox10.UseVisualStyleBackColor = true;
            this.checkBox10.CheckedChanged += new System.EventHandler(this.checkBox10_CheckedChanged);
            //
            // groupBox1
            //
            this.groupBox1.Controls.Add(this.label90);
            this.groupBox1.Controls.Add(this.TidalFileName);
            this.groupBox1.Controls.Add(this.TidalInputStep);
            this.groupBox1.Controls.Add(this.label82);
            this.groupBox1.Controls.Add(this.TidalYmin);
            this.groupBox1.Controls.Add(this.TidalYmax);
            this.groupBox1.Controls.Add(this.TidalXmax);
            this.groupBox1.Controls.Add(this.TidalXmin);
            this.groupBox1.Controls.Add(this.label80);
            this.groupBox1.Controls.Add(this.label81);
            this.groupBox1.Location = new System.Drawing.Point(526, 309);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(427, 141);
            this.groupBox1.TabIndex = 213;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Stage/Tidal input variables";
            //
            // label90
            //
            this.label90.Location = new System.Drawing.Point(206, 45);
            this.label90.Name = "label90";
            this.label90.Size = new System.Drawing.Size(60, 15);
            this.label90.TabIndex = 212;
            this.label90.Text = "File Name";
            //
            // TidalFileName
            //
            this.TidalFileName.Location = new System.Drawing.Point(207, 62);
            this.TidalFileName.Name = "TidalFileName";
            this.TidalFileName.Size = new System.Drawing.Size(120, 20);
            this.TidalFileName.TabIndex = 211;
            //
            // TidalInputStep
            //
            this.TidalInputStep.Location = new System.Drawing.Point(264, 97);
            this.TidalInputStep.Name = "TidalInputStep";
            this.TidalInputStep.Size = new System.Drawing.Size(63, 20);
            this.TidalInputStep.TabIndex = 209;
            this.TidalInputStep.Text = "1440";
            //
            // label82
            //
            this.label82.Location = new System.Drawing.Point(160, 90);
            this.label82.Name = "label82";
            this.label82.Size = new System.Drawing.Size(100, 32);
            this.label82.TabIndex = 210;
            this.label82.Text = "input data time step (mins)";
            this.label82.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // TidalYmin
            //
            this.TidalYmin.Location = new System.Drawing.Point(93, 39);
            this.TidalYmin.Name = "TidalYmin";
            this.TidalYmin.Size = new System.Drawing.Size(32, 20);
            this.TidalYmin.TabIndex = 208;
            this.TidalYmin.Text = "0";
            //
            // TidalYmax
            //
            this.TidalYmax.Location = new System.Drawing.Point(93, 87);
            this.TidalYmax.Name = "TidalYmax";
            this.TidalYmax.Size = new System.Drawing.Size(32, 20);
            this.TidalYmax.TabIndex = 207;
            this.TidalYmax.Text = "0";
            //
            // TidalXmax
            //
            this.TidalXmax.Location = new System.Drawing.Point(128, 62);
            this.TidalXmax.Name = "TidalXmax";
            this.TidalXmax.Size = new System.Drawing.Size(31, 20);
            this.TidalXmax.TabIndex = 206;
            this.TidalXmax.Text = "0";
            //
            // TidalXmin
            //
            this.TidalXmin.Location = new System.Drawing.Point(54, 62);
            this.TidalXmin.Name = "TidalXmin";
            this.TidalXmin.Size = new System.Drawing.Size(31, 20);
            this.TidalXmin.TabIndex = 205;
            this.TidalXmin.Text = "0";
            //
            // label80
            //
            this.label80.Location = new System.Drawing.Point(100, 21);
            this.label80.Name = "label80";
            this.label80.Size = new System.Drawing.Size(15, 15);
            this.label80.TabIndex = 204;
            this.label80.Text = "Y";
            //
            // label81
            //
            this.label81.Location = new System.Drawing.Point(32, 65);
            this.label81.Name = "label81";
            this.label81.Size = new System.Drawing.Size(16, 15);
            this.label81.TabIndex = 203;
            this.label81.Text = "X";
            //
            // tabPage2
            //
            this.tabPage2.Controls.Add(this.groupBox8);
            this.tabPage2.Controls.Add(this.veg_lat_box);
            this.tabPage2.Controls.Add(this.label51);
            this.tabPage2.Controls.Add(this.grasstextbox);
            this.tabPage2.Controls.Add(this.label40);
            this.tabPage2.Controls.Add(this.label36);
            this.tabPage2.Controls.Add(this.vegTauCritBox);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Size = new System.Drawing.Size(1323, 504);
            this.tabPage2.TabIndex = 7;
            this.tabPage2.Text = "Vegetation";
            this.tabPage2.UseVisualStyleBackColor = true;
            //
            // groupBox8
            //
            this.groupBox8.Controls.Add(this.radioButton2);
            this.groupBox8.Controls.Add(this.radioButton1);
            this.groupBox8.Location = new System.Drawing.Point(284, 32);
            this.groupBox8.Name = "groupBox8";
            this.groupBox8.Size = new System.Drawing.Size(117, 77);
            this.groupBox8.TabIndex = 178;
            this.groupBox8.TabStop = false;
            this.groupBox8.Text = "Vegetation model";
            //
            // radioButton2
            //
            this.radioButton2.AutoSize = true;
            this.radioButton2.Location = new System.Drawing.Point(17, 43);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(84, 17);
            this.radioButton2.TabIndex = 177;
            this.radioButton2.Text = "new (>=1.9f)";
            this.radioButton2.UseVisualStyleBackColor = true;
            //
            // radioButton1
            //
            this.radioButton1.AutoSize = true;
            this.radioButton1.Checked = true;
            this.radioButton1.Location = new System.Drawing.Point(17, 20);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(75, 17);
            this.radioButton1.TabIndex = 176;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "old (<1.9d)";
            this.radioButton1.UseVisualStyleBackColor = true;
            //
            // veg_lat_box
            //
            this.veg_lat_box.Location = new System.Drawing.Point(157, 117);
            this.veg_lat_box.Name = "veg_lat_box";
            this.veg_lat_box.Size = new System.Drawing.Size(40, 20);
            this.veg_lat_box.TabIndex = 175;
            this.veg_lat_box.Text = "0.1";
            //
            // label51
            //
            this.label51.Location = new System.Drawing.Point(29, 103);
            this.label51.Name = "label51";
            this.label51.Size = new System.Drawing.Size(120, 47);
            this.label51.TabIndex = 174;
            this.label51.Text = "Proportion of erosion that can occur when veg is fully grown (0-1)";
            this.label51.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // grasstextbox
            //
            this.grasstextbox.Location = new System.Drawing.Point(157, 68);
            this.grasstextbox.Name = "grasstextbox";
            this.grasstextbox.Size = new System.Drawing.Size(40, 20);
            this.grasstextbox.TabIndex = 172;
            this.grasstextbox.Text = "5";
            //
            // label40
            //
            this.label40.Location = new System.Drawing.Point(29, 68);
            this.label40.Name = "label40";
            this.label40.Size = new System.Drawing.Size(120, 24);
            this.label40.TabIndex = 173;
            this.label40.Text = "Grass maturity (yrs)";
            this.label40.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label36
            //
            this.label36.Location = new System.Drawing.Point(37, 28);
            this.label36.Name = "label36";
            this.label36.Size = new System.Drawing.Size(112, 24);
            this.label36.TabIndex = 108;
            this.label36.Text = "vegetation crit shear";
            this.label36.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // vegTauCritBox
            //
            this.vegTauCritBox.Location = new System.Drawing.Point(157, 32);
            this.vegTauCritBox.Name = "vegTauCritBox";
            this.vegTauCritBox.Size = new System.Drawing.Size(71, 20);
            this.vegTauCritBox.TabIndex = 92;
            this.vegTauCritBox.Text = "180.0";
            //
            // tabPage4
            //
            this.tabPage4.Controls.Add(this.angle_thresholdbox);
            this.tabPage4.Controls.Add(this.soilerosionBox);
            this.tabPage4.Controls.Add(this.landslidesBox);
            this.tabPage4.Controls.Add(this.label75);
            this.tabPage4.Controls.Add(this.label74);
            this.tabPage4.Controls.Add(this.label73);
            this.tabPage4.Controls.Add(this.label72);
            this.tabPage4.Controls.Add(this.label71);
            this.tabPage4.Controls.Add(this.m3Box);
            this.tabPage4.Controls.Add(this.n1Box);
            this.tabPage4.Controls.Add(this.m1Box);
            this.tabPage4.Controls.Add(this.Beta3Box);
            this.tabPage4.Controls.Add(this.Beta1Box);
            this.tabPage4.Controls.Add(this.SiberiaBox);
            this.tabPage4.Controls.Add(this.label70);
            this.tabPage4.Controls.Add(this.label69);
            this.tabPage4.Controls.Add(this.label68);
            this.tabPage4.Controls.Add(this.label67);
            this.tabPage4.Controls.Add(this.soil_ratebox);
            this.tabPage4.Controls.Add(this.slopebox);
            this.tabPage4.Controls.Add(this.creepratebox);
            this.tabPage4.Controls.Add(this.label34);
            this.tabPage4.Controls.Add(this.label8);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(1323, 504);
            this.tabPage4.TabIndex = 9;
            this.tabPage4.Text = "Slope Processes";
            this.tabPage4.UseVisualStyleBackColor = true;
            //
            // angle_thresholdbox
            //
            this.angle_thresholdbox.Location = new System.Drawing.Point(1170, 124);
            this.angle_thresholdbox.Name = "angle_thresholdbox";
            this.angle_thresholdbox.Size = new System.Drawing.Size(132, 20);
            this.angle_thresholdbox.TabIndex = 188;
            this.angle_thresholdbox.Text = "null";
            //
            // soilerosionBox
            //
            this.soilerosionBox.Location = new System.Drawing.Point(223, 163);
            this.soilerosionBox.Name = "soilerosionBox";
            this.soilerosionBox.Size = new System.Drawing.Size(147, 34);
            this.soilerosionBox.TabIndex = 187;
            this.soilerosionBox.Text = "Soil erosion varies according to j_mean";
            this.toolTip1.SetToolTip(this.soilerosionBox, "CAESAR can run in both catchment and reach mode, but if you have catchment mode c" +
        "hecked, you should input a rainfall data file");
            //
            // landslidesBox
            //
            this.landslidesBox.Location = new System.Drawing.Point(223, 69);
            this.landslidesBox.Name = "landslidesBox";
            this.landslidesBox.Size = new System.Drawing.Size(196, 34);
            this.landslidesBox.TabIndex = 186;
            this.landslidesBox.Text = "Dynamic Slope fail angle -  varies according to j_mean";
            //
            // label75
            //
            this.label75.Location = new System.Drawing.Point(648, 174);
            this.label75.Name = "label75";
            this.label75.Size = new System.Drawing.Size(96, 24);
            this.label75.TabIndex = 185;
            this.label75.Text = "n1";
            this.label75.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label74
            //
            this.label74.Location = new System.Drawing.Point(648, 151);
            this.label74.Name = "label74";
            this.label74.Size = new System.Drawing.Size(96, 24);
            this.label74.TabIndex = 184;
            this.label74.Text = "m3";
            this.label74.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label73
            //
            this.label73.Location = new System.Drawing.Point(648, 73);
            this.label73.Name = "label73";
            this.label73.Size = new System.Drawing.Size(96, 24);
            this.label73.TabIndex = 183;
            this.label73.Text = "Beta1";
            this.label73.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label72
            //
            this.label72.Location = new System.Drawing.Point(648, 125);
            this.label72.Name = "label72";
            this.label72.Size = new System.Drawing.Size(96, 24);
            this.label72.TabIndex = 182;
            this.label72.Text = "m1";
            this.label72.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label71
            //
            this.label71.Location = new System.Drawing.Point(648, 99);
            this.label71.Name = "label71";
            this.label71.Size = new System.Drawing.Size(96, 24);
            this.label71.TabIndex = 181;
            this.label71.Text = "Beta3";
            this.label71.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // m3Box
            //
            this.m3Box.Location = new System.Drawing.Point(750, 151);
            this.m3Box.Name = "m3Box";
            this.m3Box.Size = new System.Drawing.Size(49, 20);
            this.m3Box.TabIndex = 180;
            this.m3Box.Text = "0.79";
            //
            // n1Box
            //
            this.n1Box.Location = new System.Drawing.Point(750, 177);
            this.n1Box.Name = "n1Box";
            this.n1Box.Size = new System.Drawing.Size(49, 20);
            this.n1Box.TabIndex = 179;
            this.n1Box.Text = "0.69";
            this.n1Box.TextChanged += new System.EventHandler(this.n1Box_TextChanged);
            //
            // m1Box
            //
            this.m1Box.Location = new System.Drawing.Point(750, 125);
            this.m1Box.Name = "m1Box";
            this.m1Box.Size = new System.Drawing.Size(49, 20);
            this.m1Box.TabIndex = 178;
            this.m1Box.Text = "1.7";
            //
            // Beta3Box
            //
            this.Beta3Box.Location = new System.Drawing.Point(750, 99);
            this.Beta3Box.Name = "Beta3Box";
            this.Beta3Box.Size = new System.Drawing.Size(49, 20);
            this.Beta3Box.TabIndex = 177;
            this.Beta3Box.Text = "0.000186";
            //
            // Beta1Box
            //
            this.Beta1Box.Location = new System.Drawing.Point(750, 73);
            this.Beta1Box.Name = "Beta1Box";
            this.Beta1Box.Size = new System.Drawing.Size(49, 20);
            this.Beta1Box.TabIndex = 176;
            this.Beta1Box.Text = "1067";
            //
            // SiberiaBox
            //
            this.SiberiaBox.Location = new System.Drawing.Point(692, 36);
            this.SiberiaBox.Name = "SiberiaBox";
            this.SiberiaBox.Size = new System.Drawing.Size(147, 34);
            this.SiberiaBox.TabIndex = 175;
            this.SiberiaBox.Text = "SIBERIA sub model?";
            this.toolTip1.SetToolTip(this.SiberiaBox, "CAESAR can run in both catchment and reach mode, but if you have catchment mode c" +
        "hecked, you should input a rainfall data file");
            //
            // label70
            //
            this.label70.AutoSize = true;
            this.label70.Location = new System.Drawing.Point(220, 132);
            this.label70.Name = "label70";
            this.label70.Size = new System.Drawing.Size(246, 13);
            this.label70.TabIndex = 168;
            this.label70.Text = "simply slope * slope length, so replicates wash term";
            //
            // label69
            //
            this.label69.AutoSize = true;
            this.label69.Location = new System.Drawing.Point(220, 116);
            this.label69.Name = "label69";
            this.label69.Size = new System.Drawing.Size(357, 13);
            this.label69.TabIndex = 167;
            this.label69.Text = "Slope * Soil erosion rate * (drainage area ^ 0.5) * Time(years) / DX(cellsize)";
            //
            // label68
            //
            this.label68.AutoSize = true;
            this.label68.Location = new System.Drawing.Point(220, 42);
            this.label68.Name = "label68";
            this.label68.Size = new System.Drawing.Size(220, 13);
            this.label68.TabIndex = 166;
            this.label68.Text = "Slope * Creeprate * Time(years) / DX(cellsize)";
            //
            // label67
            //
            this.label67.Location = new System.Drawing.Point(51, 109);
            this.label67.Name = "label67";
            this.label67.Size = new System.Drawing.Size(96, 24);
            this.label67.TabIndex = 165;
            this.label67.Text = "Soil erosion rate";
            this.label67.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // soil_ratebox
            //
            this.soil_ratebox.Location = new System.Drawing.Point(155, 113);
            this.soil_ratebox.Name = "soil_ratebox";
            this.soil_ratebox.Size = new System.Drawing.Size(49, 20);
            this.soil_ratebox.TabIndex = 164;
            this.soil_ratebox.Text = "0.0";
            //
            // slopebox
            //
            this.slopebox.Location = new System.Drawing.Point(155, 75);
            this.slopebox.Name = "slopebox";
            this.slopebox.Size = new System.Drawing.Size(40, 20);
            this.slopebox.TabIndex = 162;
            this.slopebox.Text = "45";
            //
            // creepratebox
            //
            this.creepratebox.Location = new System.Drawing.Point(155, 39);
            this.creepratebox.Name = "creepratebox";
            this.creepratebox.Size = new System.Drawing.Size(49, 20);
            this.creepratebox.TabIndex = 160;
            this.creepratebox.Text = "0.0025";
            //
            // label34
            //
            this.label34.Location = new System.Drawing.Point(21, 75);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(128, 24);
            this.label34.TabIndex = 163;
            this.label34.Text = "slope failure threshold";
            this.label34.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label8
            //
            this.label8.Location = new System.Drawing.Point(51, 39);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(96, 24);
            this.label8.TabIndex = 161;
            this.label8.Text = "Creep rate";
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // tabPage5
            //
            this.tabPage5.Controls.Add(this.label91);
            this.tabPage5.Controls.Add(this.textBox10);
            this.tabPage5.Controls.Add(this.fraction_dune);
            this.tabPage5.Controls.Add(this.label57);
            this.tabPage5.Controls.Add(this.dune_grid_size_box);
            this.tabPage5.Controls.Add(this.label56);
            this.tabPage5.Controls.Add(this.dune_time_box);
            this.tabPage5.Controls.Add(this.label89);
            this.tabPage5.Controls.Add(this.label88);
            this.tabPage5.Controls.Add(this.label87);
            this.tabPage5.Controls.Add(this.label86);
            this.tabPage5.Controls.Add(this.label85);
            this.tabPage5.Controls.Add(this.label84);
            this.tabPage5.Controls.Add(this.slab_depth_box);
            this.tabPage5.Controls.Add(this.shadow_angle_box);
            this.tabPage5.Controls.Add(this.upstream_check_box);
            this.tabPage5.Controls.Add(this.depo_prob_box);
            this.tabPage5.Controls.Add(this.offset_box);
            this.tabPage5.Controls.Add(this.init_depth_box);
            this.tabPage5.Controls.Add(this.DuneBox);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(1323, 504);
            this.tabPage5.TabIndex = 10;
            this.tabPage5.Text = "Dune model";
            this.tabPage5.UseVisualStyleBackColor = true;
            this.tabPage5.Click += new System.EventHandler(this.tabPage5_Click);
            //
            // label91
            //
            this.label91.AutoSize = true;
            this.label91.Location = new System.Drawing.Point(115, 153);
            this.label91.Name = "label91";
            this.label91.Size = new System.Drawing.Size(100, 13);
            this.label91.TabIndex = 19;
            this.label91.Text = "Dune landslip angle";
            //
            // textBox10
            //
            this.textBox10.Location = new System.Drawing.Point(26, 150);
            this.textBox10.Name = "textBox10";
            this.textBox10.Size = new System.Drawing.Size(75, 20);
            this.textBox10.TabIndex = 18;
            this.textBox10.Text = "30";
            //
            // fraction_dune
            //
            this.fraction_dune.Location = new System.Drawing.Point(512, 113);
            this.fraction_dune.Name = "fraction_dune";
            this.fraction_dune.Size = new System.Drawing.Size(75, 20);
            this.fraction_dune.TabIndex = 17;
            this.fraction_dune.Text = "1";
            //
            // label57
            //
            this.label57.AutoSize = true;
            this.label57.Location = new System.Drawing.Point(600, 79);
            this.label57.Name = "label57";
            this.label57.Size = new System.Drawing.Size(91, 13);
            this.label57.TabIndex = 16;
            this.label57.Text = "Grid size of dunes";
            //
            // dune_grid_size_box
            //
            this.dune_grid_size_box.Location = new System.Drawing.Point(512, 76);
            this.dune_grid_size_box.Name = "dune_grid_size_box";
            this.dune_grid_size_box.Size = new System.Drawing.Size(75, 20);
            this.dune_grid_size_box.TabIndex = 15;
            this.dune_grid_size_box.Text = "2";
            //
            // label56
            //
            this.label56.AutoSize = true;
            this.label56.Location = new System.Drawing.Point(600, 44);
            this.label56.Name = "label56";
            this.label56.Size = new System.Drawing.Size(169, 13);
            this.label56.TabIndex = 14;
            this.label56.Text = "time step (min) between dune calls";
            //
            // dune_time_box
            //
            this.dune_time_box.Location = new System.Drawing.Point(512, 41);
            this.dune_time_box.Name = "dune_time_box";
            this.dune_time_box.Size = new System.Drawing.Size(75, 20);
            this.dune_time_box.TabIndex = 13;
            this.dune_time_box.Text = "144";
            //
            // label89
            //
            this.label89.AutoSize = true;
            this.label89.Location = new System.Drawing.Point(117, 257);
            this.label89.Name = "label89";
            this.label89.Size = new System.Drawing.Size(270, 13);
            this.label89.TabIndex = 12;
            this.label89.Text = "Downstream offset (travel distance automatically added)";
            //
            // label88
            //
            this.label88.AutoSize = true;
            this.label88.Location = new System.Drawing.Point(115, 222);
            this.label88.Name = "label88";
            this.label88.Size = new System.Drawing.Size(124, 13);
            this.label88.TabIndex = 11;
            this.label88.Text = "Deposition probability (%)";
            this.label88.Click += new System.EventHandler(this.label88_Click);
            //
            // label87
            //
            this.label87.AutoSize = true;
            this.label87.Location = new System.Drawing.Point(115, 186);
            this.label87.Name = "label87";
            this.label87.Size = new System.Drawing.Size(381, 13);
            this.label87.TabIndex = 10;
            this.label87.Text = "Shadow check distance (cells) - number of cells Upstream it checks for shadow";
            //
            // label86
            //
            this.label86.AutoSize = true;
            this.label86.Location = new System.Drawing.Point(115, 118);
            this.label86.Name = "label86";
            this.label86.Size = new System.Drawing.Size(105, 13);
            this.label86.TabIndex = 9;
            this.label86.Text = "Shadow angle (deg) ";
            //
            // label85
            //
            this.label85.AutoSize = true;
            this.label85.Location = new System.Drawing.Point(117, 83);
            this.label85.Name = "label85";
            this.label85.Size = new System.Drawing.Size(138, 13);
            this.label85.TabIndex = 8;
            this.label85.Text = "Maximum slab thickness (m)";
            //
            // label84
            //
            this.label84.AutoSize = true;
            this.label84.Location = new System.Drawing.Point(115, 47);
            this.label84.Name = "label84";
            this.label84.Size = new System.Drawing.Size(185, 13);
            this.label84.TabIndex = 7;
            this.label84.Text = "how many slabs added per col per iter";
            //
            // slab_depth_box
            //
            this.slab_depth_box.Location = new System.Drawing.Point(26, 80);
            this.slab_depth_box.Name = "slab_depth_box";
            this.slab_depth_box.Size = new System.Drawing.Size(75, 20);
            this.slab_depth_box.TabIndex = 6;
            this.slab_depth_box.Text = "0.5";
            this.slab_depth_box.TextChanged += new System.EventHandler(this.textBox12_TextChanged);
            //
            // shadow_angle_box
            //
            this.shadow_angle_box.Location = new System.Drawing.Point(26, 115);
            this.shadow_angle_box.Name = "shadow_angle_box";
            this.shadow_angle_box.Size = new System.Drawing.Size(75, 20);
            this.shadow_angle_box.TabIndex = 5;
            this.shadow_angle_box.Text = "15";
            //
            // upstream_check_box
            //
            this.upstream_check_box.Location = new System.Drawing.Point(26, 183);
            this.upstream_check_box.Name = "upstream_check_box";
            this.upstream_check_box.Size = new System.Drawing.Size(75, 20);
            this.upstream_check_box.TabIndex = 4;
            this.upstream_check_box.Text = "40";
            //
            // depo_prob_box
            //
            this.depo_prob_box.Location = new System.Drawing.Point(27, 219);
            this.depo_prob_box.Name = "depo_prob_box";
            this.depo_prob_box.Size = new System.Drawing.Size(75, 20);
            this.depo_prob_box.TabIndex = 3;
            this.depo_prob_box.Text = "50";
            //
            // offset_box
            //
            this.offset_box.Location = new System.Drawing.Point(26, 254);
            this.offset_box.Name = "offset_box";
            this.offset_box.Size = new System.Drawing.Size(75, 20);
            this.offset_box.TabIndex = 2;
            this.offset_box.Text = "1";
            //
            // init_depth_box
            //
            this.init_depth_box.Location = new System.Drawing.Point(27, 44);
            this.init_depth_box.Name = "init_depth_box";
            this.init_depth_box.Size = new System.Drawing.Size(75, 20);
            this.init_depth_box.TabIndex = 1;
            this.init_depth_box.Text = "4";
            //
            // DuneBox
            //
            this.DuneBox.AutoSize = true;
            this.DuneBox.Location = new System.Drawing.Point(26, 18);
            this.DuneBox.Name = "DuneBox";
            this.DuneBox.Size = new System.Drawing.Size(108, 17);
            this.DuneBox.TabIndex = 0;
            this.DuneBox.Text = "Run with Dunes?";
            this.DuneBox.UseVisualStyleBackColor = true;
            //
            // tabPage1
            //
            this.tabPage1.Controls.Add(this.textBox19);
            this.tabPage1.Controls.Add(this.label104);
            this.tabPage1.Controls.Add(this.SpatVarManningsCheckbox);
            this.tabPage1.Controls.Add(this.MinQmaxvalue);
            this.tabPage1.Controls.Add(this.textBox9);
            this.tabPage1.Controls.Add(this.label77);
            this.tabPage1.Controls.Add(this.textBox8);
            this.tabPage1.Controls.Add(this.label66);
            this.tabPage1.Controls.Add(this.textBox4);
            this.tabPage1.Controls.Add(this.label64);
            this.tabPage1.Controls.Add(this.courantbox);
            this.tabPage1.Controls.Add(this.label38);
            this.tabPage1.Controls.Add(this.label53);
            this.tabPage1.Controls.Add(this.Q2box);
            this.tabPage1.Controls.Add(this.label3);
            this.tabPage1.Controls.Add(this.k_evapBox);
            this.tabPage1.Controls.Add(this.textBox2);
            this.tabPage1.Controls.Add(this.label46);
            this.tabPage1.Controls.Add(this.minqbox);
            this.tabPage1.Controls.Add(this.initscansbox);
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(1323, 504);
            this.tabPage1.TabIndex = 11;
            this.tabPage1.Text = "Flow Model";
            this.tabPage1.UseVisualStyleBackColor = true;
            //
            // textBox19
            //
            this.textBox19.Location = new System.Drawing.Point(331, 341);
            this.textBox19.Name = "textBox19";
            this.textBox19.Size = new System.Drawing.Size(118, 20);
            this.textBox19.TabIndex = 235;
            this.textBox19.Text = "null";
            this.textBox19.Visible = false;
            //
            // label104
            //
            this.label104.AutoSize = true;
            this.label104.Location = new System.Drawing.Point(247, 344);
            this.label104.Name = "label104";
            this.label104.Size = new System.Drawing.Size(78, 13);
            this.label104.TabIndex = 234;
            this.label104.Text = "Mannings n file";
            this.label104.Visible = false;
            //
            // SpatVarManningsCheckbox
            //
            this.SpatVarManningsCheckbox.AutoSize = true;
            this.SpatVarManningsCheckbox.Location = new System.Drawing.Point(293, 318);
            this.SpatVarManningsCheckbox.Name = "SpatVarManningsCheckbox";
            this.SpatVarManningsCheckbox.Size = new System.Drawing.Size(162, 17);
            this.SpatVarManningsCheckbox.TabIndex = 230;
            this.SpatVarManningsCheckbox.Text = "Spatially variable mannings n";
            this.SpatVarManningsCheckbox.UseVisualStyleBackColor = true;
            this.SpatVarManningsCheckbox.CheckedChanged += new System.EventHandler(this.SpatVarManningsCheckbox_CheckedChanged);
            //
            // MinQmaxvalue
            //
            this.MinQmaxvalue.Location = new System.Drawing.Point(302, 106);
            this.MinQmaxvalue.Name = "MinQmaxvalue";
            this.MinQmaxvalue.Size = new System.Drawing.Size(49, 20);
            this.MinQmaxvalue.TabIndex = 216;
            this.MinQmaxvalue.Text = "1000.0";
            //
            // textBox9
            //
            this.textBox9.Location = new System.Drawing.Point(229, 315);
            this.textBox9.Name = "textBox9";
            this.textBox9.Size = new System.Drawing.Size(38, 20);
            this.textBox9.TabIndex = 215;
            this.textBox9.Text = "0.04";
            //
            // label77
            //
            this.label77.Location = new System.Drawing.Point(95, 311);
            this.label77.Name = "label77";
            this.label77.Size = new System.Drawing.Size(128, 24);
            this.label77.TabIndex = 214;
            this.label77.Text = "Mannings n";
            this.label77.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // textBox8
            //
            this.textBox8.Location = new System.Drawing.Point(229, 291);
            this.textBox8.Name = "textBox8";
            this.textBox8.Size = new System.Drawing.Size(38, 20);
            this.textBox8.TabIndex = 213;
            this.textBox8.Text = "0.8";
            //
            // label66
            //
            this.label66.Location = new System.Drawing.Point(95, 287);
            this.label66.Name = "label66";
            this.label66.Size = new System.Drawing.Size(128, 24);
            this.label66.TabIndex = 212;
            this.label66.Text = "froude # flow limit";
            this.label66.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // textBox4
            //
            this.textBox4.Location = new System.Drawing.Point(229, 267);
            this.textBox4.Name = "textBox4";
            this.textBox4.Size = new System.Drawing.Size(38, 20);
            this.textBox4.TabIndex = 211;
            this.textBox4.Text = "0.00001";
            //
            // label64
            //
            this.label64.Location = new System.Drawing.Point(95, 263);
            this.label64.Name = "label64";
            this.label64.Size = new System.Drawing.Size(128, 24);
            this.label64.TabIndex = 210;
            this.label64.Text = "hflow threshold";
            this.label64.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // courantbox
            //
            this.courantbox.Location = new System.Drawing.Point(229, 241);
            this.courantbox.Name = "courantbox";
            this.courantbox.Size = new System.Drawing.Size(38, 20);
            this.courantbox.TabIndex = 209;
            this.courantbox.Text = "0.7";
            //
            // label38
            //
            this.label38.Location = new System.Drawing.Point(95, 237);
            this.label38.Name = "label38";
            this.label38.Size = new System.Drawing.Size(128, 24);
            this.label38.TabIndex = 208;
            this.label38.Text = "Courant Number";
            this.label38.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // label53
            //
            this.label53.Location = new System.Drawing.Point(101, 130);
            this.label53.Name = "label53";
            this.label53.Size = new System.Drawing.Size(120, 43);
            this.label53.TabIndex = 205;
            this.label53.Text = "Water depth threshold above which erosion will happen (m)";
            this.label53.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label53, "MinQ is a threshold value, above which CAEASR treats flow as surface flow, it sho" +
        "uld be scaled to the grid size. Try 0.01 for 10m grids and 0.25 for 50m..");
            //
            // Q2box
            //
            this.Q2box.Location = new System.Drawing.Point(229, 137);
            this.Q2box.Name = "Q2box";
            this.Q2box.Size = new System.Drawing.Size(49, 20);
            this.Q2box.TabIndex = 204;
            this.Q2box.Text = "0.01";
            //
            // label3
            //
            this.label3.Location = new System.Drawing.Point(82, 212);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(139, 24);
            this.label3.TabIndex = 203;
            this.label3.Text = "Evaporation rate (m/day)";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // k_evapBox
            //
            this.k_evapBox.Location = new System.Drawing.Point(229, 215);
            this.k_evapBox.Name = "k_evapBox";
            this.k_evapBox.Size = new System.Drawing.Size(80, 20);
            this.k_evapBox.TabIndex = 202;
            this.k_evapBox.Text = "0.0";
            //
            // textBox2
            //
            this.textBox2.Location = new System.Drawing.Point(229, 182);
            this.textBox2.Name = "textBox2";
            this.textBox2.Size = new System.Drawing.Size(40, 20);
            this.textBox2.TabIndex = 200;
            this.textBox2.Text = "0.005";
            //
            // label46
            //
            this.label46.Location = new System.Drawing.Point(93, 179);
            this.label46.Name = "label46";
            this.label46.Size = new System.Drawing.Size(128, 24);
            this.label46.TabIndex = 201;
            this.label46.Text = "Slope for edge cells";
            this.label46.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // minqbox
            //
            this.minqbox.Location = new System.Drawing.Point(229, 106);
            this.minqbox.Name = "minqbox";
            this.minqbox.Size = new System.Drawing.Size(49, 20);
            this.minqbox.TabIndex = 197;
            this.minqbox.Text = "0.01";
            //
            // initscansbox
            //
            this.initscansbox.Location = new System.Drawing.Point(227, 68);
            this.initscansbox.Name = "initscansbox";
            this.initscansbox.Size = new System.Drawing.Size(40, 20);
            this.initscansbox.TabIndex = 196;
            this.initscansbox.Text = "1";
            //
            // label9
            //
            this.label9.Location = new System.Drawing.Point(101, 106);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(120, 24);
            this.label9.TabIndex = 199;
            this.label9.Text = "Min Q for depth calc";
            this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.toolTip1.SetToolTip(this.label9, "MinQ is a threshold value, above which CAEASR treats flow as surface flow, it sho" +
        "uld be scaled to the grid size. Try 0.01 for 10m grids and 0.25 for 50m..");
            //
            // label5
            //
            this.label5.Location = new System.Drawing.Point(29, 65);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(182, 26);
            this.label5.TabIndex = 198;
            this.label5.Text = "input/output difference allowed";
            this.label5.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            //
            // tabPage3
            //
            this.tabPage3.Controls.Add(this.label101);
            this.tabPage3.Controls.Add(this.label100);
            this.tabPage3.Controls.Add(this.label99);
            this.tabPage3.Controls.Add(this.textBox18);
            this.tabPage3.Controls.Add(this.textBox17);
            this.tabPage3.Controls.Add(this.textBox16);
            this.tabPage3.Controls.Add(this.label98);
            this.tabPage3.Controls.Add(this.label97);
            this.tabPage3.Controls.Add(this.label96);
            this.tabPage3.Controls.Add(this.textBox15);
            this.tabPage3.Controls.Add(this.textBox14);
            this.tabPage3.Controls.Add(this.textBox13);
            this.tabPage3.Controls.Add(this.label95);
            this.tabPage3.Controls.Add(this.label94);
            this.tabPage3.Controls.Add(this.textBox12);
            this.tabPage3.Controls.Add(this.textBox11);
            this.tabPage3.Controls.Add(this.checkBox6);
            this.tabPage3.Controls.Add(this.checkBox5);
            this.tabPage3.Controls.Add(this.checkBox4);
            this.tabPage3.Controls.Add(this.soildevbox);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(1323, 504);
            this.tabPage3.TabIndex = 12;
            this.tabPage3.Text = "Soil Development";
            this.tabPage3.UseVisualStyleBackColor = true;
            //
            // label101
            //
            this.label101.AutoSize = true;
            this.label101.Enabled = false;
            this.label101.Location = new System.Drawing.Point(414, 153);
            this.label101.Name = "label101";
            this.label101.Size = new System.Drawing.Size(19, 13);
            this.label101.TabIndex = 19;
            this.label101.Text = "c4";
            //
            // label100
            //
            this.label100.AutoSize = true;
            this.label100.Enabled = false;
            this.label100.Location = new System.Drawing.Point(414, 124);
            this.label100.Name = "label100";
            this.label100.Size = new System.Drawing.Size(19, 13);
            this.label100.TabIndex = 18;
            this.label100.Text = "c3";
            //
            // label99
            //
            this.label99.AutoSize = true;
            this.label99.Enabled = false;
            this.label99.Location = new System.Drawing.Point(415, 94);
            this.label99.Name = "label99";
            this.label99.Size = new System.Drawing.Size(19, 13);
            this.label99.TabIndex = 17;
            this.label99.Text = "k2";
            //
            // textBox18
            //
            this.textBox18.Enabled = false;
            this.textBox18.Location = new System.Drawing.Point(336, 150);
            this.textBox18.Name = "textBox18";
            this.textBox18.Size = new System.Drawing.Size(57, 20);
            this.textBox18.TabIndex = 16;
            this.textBox18.Text = "1";
            //
            // textBox17
            //
            this.textBox17.Enabled = false;
            this.textBox17.Location = new System.Drawing.Point(336, 121);
            this.textBox17.Name = "textBox17";
            this.textBox17.Size = new System.Drawing.Size(57, 20);
            this.textBox17.TabIndex = 15;
            this.textBox17.Text = "-2.5";
            //
            // textBox16
            //
            this.textBox16.Enabled = false;
            this.textBox16.Location = new System.Drawing.Point(337, 91);
            this.textBox16.Name = "textBox16";
            this.textBox16.Size = new System.Drawing.Size(57, 20);
            this.textBox16.TabIndex = 14;
            this.textBox16.Text = "70";
            //
            // label98
            //
            this.label98.AutoSize = true;
            this.label98.Location = new System.Drawing.Point(99, 240);
            this.label98.Name = "label98";
            this.label98.Size = new System.Drawing.Size(19, 13);
            this.label98.TabIndex = 13;
            this.label98.Text = "c2";
            //
            // label97
            //
            this.label97.AutoSize = true;
            this.label97.Location = new System.Drawing.Point(100, 207);
            this.label97.Name = "label97";
            this.label97.Size = new System.Drawing.Size(19, 13);
            this.label97.TabIndex = 12;
            this.label97.Text = "c1";
            //
            // label96
            //
            this.label96.AutoSize = true;
            this.label96.Location = new System.Drawing.Point(101, 173);
            this.label96.Name = "label96";
            this.label96.Size = new System.Drawing.Size(19, 13);
            this.label96.TabIndex = 11;
            this.label96.Text = "k1";
            //
            // textBox15
            //
            this.textBox15.Location = new System.Drawing.Point(32, 238);
            this.textBox15.Name = "textBox15";
            this.textBox15.Size = new System.Drawing.Size(50, 20);
            this.textBox15.TabIndex = 10;
            this.textBox15.Text = "5";
            //
            // textBox14
            //
            this.textBox14.Location = new System.Drawing.Point(33, 205);
            this.textBox14.Name = "textBox14";
            this.textBox14.Size = new System.Drawing.Size(50, 20);
            this.textBox14.TabIndex = 9;
            this.textBox14.Text = "-0.5";
            //
            // textBox13
            //
            this.textBox13.Location = new System.Drawing.Point(32, 173);
            this.textBox13.Name = "textBox13";
            this.textBox13.Size = new System.Drawing.Size(50, 20);
            this.textBox13.TabIndex = 8;
            this.textBox13.Text = "0.0001";
            //
            // label95
            //
            this.label95.AutoSize = true;
            this.label95.Location = new System.Drawing.Point(98, 121);
            this.label95.Name = "label95";
            this.label95.Size = new System.Drawing.Size(19, 13);
            this.label95.TabIndex = 7;
            this.label95.Text = "b1";
            //
            // label94
            //
            this.label94.AutoSize = true;
            this.label94.Location = new System.Drawing.Point(97, 94);
            this.label94.Name = "label94";
            this.label94.Size = new System.Drawing.Size(20, 13);
            this.label94.TabIndex = 6;
            this.label94.Text = "P1";
            //
            // textBox12
            //
            this.textBox12.Location = new System.Drawing.Point(34, 120);
            this.textBox12.Name = "textBox12";
            this.textBox12.Size = new System.Drawing.Size(50, 20);
            this.textBox12.TabIndex = 5;
            this.textBox12.Text = "2";
            //
            // textBox11
            //
            this.textBox11.Location = new System.Drawing.Point(34, 91);
            this.textBox11.Name = "textBox11";
            this.textBox11.Size = new System.Drawing.Size(51, 20);
            this.textBox11.TabIndex = 4;
            this.textBox11.Text = "0.000053";
            //
            // checkBox6
            //
            this.checkBox6.AutoSize = true;
            this.checkBox6.Enabled = false;
            this.checkBox6.Location = new System.Drawing.Point(336, 61);
            this.checkBox6.Name = "checkBox6";
            this.checkBox6.Size = new System.Drawing.Size(127, 17);
            this.checkBox6.TabIndex = 3;
            this.checkBox6.Text = "Chemical Weathering";
            this.checkBox6.UseVisualStyleBackColor = true;
            //
            // checkBox5
            //
            this.checkBox5.AutoSize = true;
            this.checkBox5.Location = new System.Drawing.Point(35, 150);
            this.checkBox5.Name = "checkBox5";
            this.checkBox5.Size = new System.Drawing.Size(123, 17);
            this.checkBox5.TabIndex = 2;
            this.checkBox5.Text = "Physical Weathering";
            this.checkBox5.UseVisualStyleBackColor = true;
            //
            // checkBox4
            //
            this.checkBox4.AutoSize = true;
            this.checkBox4.Location = new System.Drawing.Point(33, 63);
            this.checkBox4.Name = "checkBox4";
            this.checkBox4.Size = new System.Drawing.Size(108, 17);
            this.checkBox4.TabIndex = 1;
            this.checkBox4.Text = "Bedrock lowering";
            this.checkBox4.UseVisualStyleBackColor = true;
            //
            // soildevbox
            //
            this.soildevbox.AutoSize = true;
            this.soildevbox.Location = new System.Drawing.Point(32, 16);
            this.soildevbox.Name = "soildevbox";
            this.soildevbox.Size = new System.Drawing.Size(147, 17);
            this.soildevbox.TabIndex = 0;
            this.soildevbox.Text = "Soil Development Model?";
            this.soildevbox.UseVisualStyleBackColor = true;
            // 
            // OilTab
            // 
            this.OilTab.Controls.Add(this.OilTab_checkBox);
            this.OilTab.Controls.Add(this.OilTab_groupBox_controls);
            this.OilTab.Location = new System.Drawing.Point(4, 22);
            this.OilTab.Name = "OilTab";
            this.OilTab.Size = new System.Drawing.Size(1323, 504);
            this.OilTab.TabIndex = 5;
            this.OilTab.Text = "Oil spill";
            this.OilTab.UseVisualStyleBackColor = true;
            this.OilTab.Click += new System.EventHandler(this.OilTab_Click);
            //
            // OilTab_checkBox
            //
            this.OilTab_checkBox.AutoSize = true;
            this.OilTab_checkBox.Location = new System.Drawing.Point(20, 24);
            this.OilTab_checkBox.Name = "OilTab_checkBox";
            this.OilTab_checkBox.Size = new System.Drawing.Size(123, 17);
            this.OilTab_checkBox.TabIndex = 226;
            this.OilTab_checkBox.Text = "Oil spill simulation";
            this.toolTip1.SetToolTip(this.OilTab_checkBox, "Activate oil spill simulation (model runs slower)");
            this.OilTab_checkBox.UseVisualStyleBackColor = true;
            this.OilTab_checkBox.CheckedChanged += new System.EventHandler(this.OilTab_checkBox_CheckedChanged);
            //
            // OilTab_groupBox_controls
            //
            this.OilTab_groupBox_controls.Controls.Add(this.OilYmin);
            this.OilTab_groupBox_controls.Controls.Add(this.OilYmax);
            this.OilTab_groupBox_controls.Controls.Add(this.OilXmin);
            this.OilTab_groupBox_controls.Controls.Add(this.OilXmax);
            this.OilTab_groupBox_controls.Controls.Add(this.OilYlabel);
            this.OilTab_groupBox_controls.Controls.Add(this.OilXlabel);
            this.OilTab_groupBox_controls.Controls.Add(this.OilTextlabel);
            this.OilTab_groupBox_controls.Controls.Add(this.OilTimeMin);
            this.OilTab_groupBox_controls.Controls.Add(this.OilTimeMinlabel);
            this.OilTab_groupBox_controls.Controls.Add(this.OilDepthStart);
            this.OilTab_groupBox_controls.Controls.Add(this.OilDepthStartlabel);
            this.OilTab_groupBox_controls.Controls.Add(this.OilVolume);
            this.OilTab_groupBox_controls.Controls.Add(this.OilVolumelabel);
            this.OilTab_groupBox_controls.Controls.Add(this.OilSpillDuration);
            this.OilTab_groupBox_controls.Controls.Add(this.OilSpillDurationlabel);
            this.OilTab_groupBox_controls.Location = new System.Drawing.Point(32, 60);
            this.OilTab_groupBox_controls.Name = "OilTab_groupBox_controls";
            this.OilTab_groupBox_controls.Size = new System.Drawing.Size(320, 300);
            this.OilTab_groupBox_controls.TabIndex = 1000;
            this.OilTab_groupBox_controls.TabStop = false;
            this.OilTab_groupBox_controls.Text = "Oil spill location and timing";
            //
            // OilYmin
            //
            this.OilYmin.Location = new System.Drawing.Point(213, 39);
            this.OilYmin.Name = "OilYmin";
            this.OilYmin.Size = new System.Drawing.Size(32, 20);
            this.OilYmin.TabIndex = 1001;
            this.OilYmin.Text = "0";
            //
            // OilYmax
            //
            this.OilYmax.Location = new System.Drawing.Point(213, 87);
            this.OilYmax.Name = "OilYmax";
            this.OilYmax.Size = new System.Drawing.Size(32, 20);
            this.OilYmax.TabIndex = 1002;
            this.OilYmax.Text = "0";
            // 
            // OilXmin
            //
            this.OilXmin.Location = new System.Drawing.Point(174, 62);
            this.OilXmin.Name = "OilXmin";
            this.OilXmin.Size = new System.Drawing.Size(31, 20);
            this.OilXmin.TabIndex = 1004;
            this.OilXmin.Text = "0";
            // 
            // OilXmax
            // 
            this.OilXmax.Location = new System.Drawing.Point(248, 62);
            this.OilXmax.Name = "OilXmax";
            this.OilXmax.Size = new System.Drawing.Size(31, 20);
            this.OilXmax.TabIndex = 1003;
            this.OilXmax.Text = "0";
            // 
            // OilYlabel
            //
            this.OilYlabel.Location = new System.Drawing.Point(220, 21);
            this.OilYlabel.Name = "OilYlabel";
            this.OilYlabel.Size = new System.Drawing.Size(15, 15);
            this.OilYlabel.TabIndex = 1005;
            this.OilYlabel.Text = "Y";
            //
            // OilXlabel
            //
            this.OilXlabel.Location = new System.Drawing.Point(152, 65);
            this.OilXlabel.Name = "OilXlabel";
            this.OilXlabel.Size = new System.Drawing.Size(16, 15);
            this.OilXlabel.TabIndex = 1006;
            this.OilXlabel.Text = "X";
            //
            // OilTextlabel
            //
            this.OilTextlabel.Location = new System.Drawing.Point(32, 50);
            this.OilTextlabel.Name = "OilTextlabel";
            this.OilTextlabel.Size = new System.Drawing.Size(130, 50);
            this.OilTextlabel.TabIndex = 1007;
            this.OilTextlabel.Text = "Location of spill:";
            //
            // OilTimeMin
            //
            this.OilTimeMin.Location = new System.Drawing.Point(213, 150);
            this.OilTimeMin.Name = "OilTimeMin";
            this.OilTimeMin.Size = new System.Drawing.Size(31, 20);
            this.OilTimeMin.TabIndex = 1008;
            this.OilTimeMin.Text = "0";
            //
            // OilTimeMinlabel
            //
            this.OilTimeMinlabel.Location = new System.Drawing.Point(32, 150);
            this.OilTimeMinlabel.Name = "OilTimeMinlabel";
            this.OilTimeMinlabel.Size = new System.Drawing.Size(130, 30);
            this.OilTimeMinlabel.TabIndex = 1009;
            this.OilTimeMinlabel.Text = "Earliest start time (s):";
            //
            // OilDepthStart
            //
            this.OilDepthStart.Location = new System.Drawing.Point(213, 180);
            this.OilDepthStart.Name = "OilDepthStart";
            this.OilDepthStart.Size = new System.Drawing.Size(31, 20);
            this.OilDepthStart.TabIndex = 1010;
            this.OilDepthStart.Text = "1.5";
            //
            // OilDepthStartlabel
            //
            this.OilDepthStartlabel.Location = new System.Drawing.Point(32, 180);
            this.OilDepthStartlabel.Name = "OilTimeMinlabel";
            this.OilDepthStartlabel.Size = new System.Drawing.Size(130, 30);
            this.OilDepthStartlabel.TabIndex = 1011;
            this.OilDepthStartlabel.Text = "Start at critical depth (m):";
            //
            // OilVolume
            //
            this.OilVolume.Location = new System.Drawing.Point(213, 210);
            this.OilVolume.Name = "OilVolume";
            this.OilVolume.Size = new System.Drawing.Size(31, 20);
            this.OilVolume.TabIndex = 1012;
            this.OilVolume.Text = "0";
            //
            // OilVolumelabel
            //
            this.OilVolumelabel.Location = new System.Drawing.Point(32, 210);
            this.OilVolumelabel.Name = "OilVolumelabel";
            this.OilVolumelabel.Size = new System.Drawing.Size(130, 30);
            this.OilVolumelabel.TabIndex = 1013;
            this.OilVolumelabel.Text = "Volume of oil spill (m3):";
            //
            // OilSpillDuration
            //
            this.OilSpillDuration.Location = new System.Drawing.Point(213, 240);
            this.OilSpillDuration.Name = "OilSpillDuration";
            this.OilSpillDuration.Size = new System.Drawing.Size(31, 20);
            this.OilSpillDuration.TabIndex = 1012;
            this.OilSpillDuration.Text = "0";
            //
            // OilSpillDurationlabel
            //
            this.OilSpillDurationlabel.Location = new System.Drawing.Point(32, 240);
            this.OilSpillDurationlabel.Name = "OilSpillDurationlabel";
            this.OilSpillDurationlabel.Size = new System.Drawing.Size(130, 30);
            this.OilSpillDurationlabel.TabIndex = 1015;
            this.OilSpillDurationlabel.Text = "Oil Spill duration (s):";
            //
            // TempTab - TEMP_V1
            //
            this.TempTab.Controls.Add(this.TempTab_checkBox);
            this.TempTab.Controls.Add(this.TempTab_label_airtemp);
            this.TempTab.Controls.Add(this.TempTab_textBox_airtemp); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_shortwave); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_shortwave); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_windspeed); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_windspeed); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_humidity); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_humidity); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_cloudcover); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_cloudcover); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_pressure); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_pressure); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_dewpoint); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_dewpoint); // TEMP_V1 
            this.TempTab.Controls.Add(this.TempTab_groupBox_scheme); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_groupBox_site); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_groupBox_initial); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_label_sourcetemp); // TEMP_V1
            this.TempTab.Controls.Add(this.TempTab_textBox_sourcetemp); // TEMP_V1
            this.TempTab.Location = new System.Drawing.Point(4, 22);
            this.TempTab.Name = "TempTab";
            this.TempTab.Size = new System.Drawing.Size(1323, 504);
            this.TempTab.TabIndex = 13;
            this.TempTab.Text = "Water Quality";
            this.TempTab.UseVisualStyleBackColor = true;
            //
            // TempTab_checkBox - TEMP_V1
            //
            this.TempTab_checkBox.AutoSize = true;
            this.TempTab_checkBox.Location = new System.Drawing.Point(20, 24);
            this.TempTab_checkBox.Name = "TempTab_checkBox";
            this.TempTab_checkBox.Size = new System.Drawing.Size(180, 17);
            this.TempTab_checkBox.TabIndex = 1;
            this.TempTab_checkBox.Text = "Simulate water temperature";
            this.TempTab_checkBox.UseVisualStyleBackColor = true;
            this.TempTab_checkBox.CheckedChanged += new System.EventHandler(this.TempTab_checkBox_CheckedChanged);
            //
            // TempTab_label_airtemp - TEMP_V1
            //
            this.TempTab_label_airtemp.AutoSize = true;
            this.TempTab_label_airtemp.Location = new System.Drawing.Point(20, 60);
            this.TempTab_label_airtemp.Name = "TempTab_label_airtemp";
            this.TempTab_label_airtemp.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_airtemp.Text = "Air temperature file";
            //
            // TempTab_textBox_airtemp - TEMP_V1
            //
            this.TempTab_textBox_airtemp.Location = new System.Drawing.Point(220, 57);
            this.TempTab_textBox_airtemp.Name = "TempTab_textBox_airtemp";
            this.TempTab_textBox_airtemp.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_airtemp.Text = "null";
            //
            // TempTab_label_shortwave - TEMP_V1
            //
            this.TempTab_label_shortwave.AutoSize = true;
            this.TempTab_label_shortwave.Location = new System.Drawing.Point(20, 87);
            this.TempTab_label_shortwave.Name = "TempTab_label_shortwave";
            this.TempTab_label_shortwave.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_shortwave.Text = "Shortwave radiation file";
            //
            // TempTab_textBox_shortwave - TEMP_V1
            //
            this.TempTab_textBox_shortwave.Location = new System.Drawing.Point(220, 84);
            this.TempTab_textBox_shortwave.Name = "TempTab_textBox_shortwave";
            this.TempTab_textBox_shortwave.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_shortwave.Text = "null";
            //
            // TempTab_label_windspeed - TEMP_V1
            //
            this.TempTab_label_windspeed.AutoSize = true;
            this.TempTab_label_windspeed.Location = new System.Drawing.Point(20, 114);
            this.TempTab_label_windspeed.Name = "TempTab_label_windspeed";
            this.TempTab_label_windspeed.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_windspeed.Text = "Wind speed file";
            //
            // TempTab_textBox_windspeed - TEMP_V1
            //
            this.TempTab_textBox_windspeed.Location = new System.Drawing.Point(220, 111);
            this.TempTab_textBox_windspeed.Name = "TempTab_textBox_windspeed";
            this.TempTab_textBox_windspeed.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_windspeed.Text = "null";
            //
            // TempTab_label_humidity - TEMP_V1
            //
            this.TempTab_label_humidity.AutoSize = true;
            this.TempTab_label_humidity.Location = new System.Drawing.Point(20, 141);
            this.TempTab_label_humidity.Name = "TempTab_label_humidity";
            this.TempTab_label_humidity.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_humidity.Text = "Relative humidity file (optional)";
            //
            // TempTab_textBox_humidity - TEMP_V1
            //
            this.TempTab_textBox_humidity.Location = new System.Drawing.Point(220, 138);
            this.TempTab_textBox_humidity.Name = "TempTab_textBox_humidity";
            this.TempTab_textBox_humidity.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_humidity.Text = "null";
            //
            // TempTab_label_cloudcover - TEMP_V1
            //
            this.TempTab_label_cloudcover.AutoSize = true;
            this.TempTab_label_cloudcover.Location = new System.Drawing.Point(20, 168);
            this.TempTab_label_cloudcover.Name = "TempTab_label_cloudcover";
            this.TempTab_label_cloudcover.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_cloudcover.Text = "Cloud cover file (optional)";
            //
            // TempTab_textBox_cloudcover - TEMP_V1
            //
            this.TempTab_textBox_cloudcover.Location = new System.Drawing.Point(220, 165);
            this.TempTab_textBox_cloudcover.Name = "TempTab_textBox_cloudcover";
            this.TempTab_textBox_cloudcover.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_cloudcover.Text = "null";
            //
            // TempTab_label_pressure - TEMP_V1
            //
            this.TempTab_label_pressure.AutoSize = true;
            this.TempTab_label_pressure.Location = new System.Drawing.Point(20, 195);
            this.TempTab_label_pressure.Name = "TempTab_label_pressure";
            this.TempTab_label_pressure.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_pressure.Text = "Atmospheric pressure file (optional)";
            //
            // TempTab_textBox_pressure - TEMP_V1
            //
            this.TempTab_textBox_pressure.Location = new System.Drawing.Point(220, 192);
            this.TempTab_textBox_pressure.Name = "TempTab_textBox_pressure";
            this.TempTab_textBox_pressure.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_pressure.Text = "null";
            //
            // TempTab_label_dewpoint - TEMP_V1
            //
            this.TempTab_label_dewpoint.AutoSize = true;
            this.TempTab_label_dewpoint.Location = new System.Drawing.Point(20, 222);
            this.TempTab_label_dewpoint.Name = "TempTab_label_dewpoint";
            this.TempTab_label_dewpoint.Size = new System.Drawing.Size(104, 13);
            this.TempTab_label_dewpoint.Text = "Dew point file (simplified scheme only)";
            //
            // TempTab_textBox_dewpoint - TEMP_V1
            //
            this.TempTab_textBox_dewpoint.Location = new System.Drawing.Point(220, 219);
            this.TempTab_textBox_dewpoint.Name = "TempTab_textBox_dewpoint";
            this.TempTab_textBox_dewpoint.Size = new System.Drawing.Size(150, 20);
            this.TempTab_textBox_dewpoint.Text = "null";
            //
            // TempTab_groupBox_scheme - TEMP_V1
            //
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_radio_fullscheme);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_radio_simplifiedscheme);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_checkBox_hecraslongwave);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_checkBox_hecrasalbedo);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_label_thermalinterval);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_textBox_thermalinterval);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_label_mettimestep);
            this.TempTab_groupBox_scheme.Controls.Add(this.TempTab_textBox_mettimestep);
            this.TempTab_groupBox_scheme.Location = new System.Drawing.Point(400, 17);
            this.TempTab_groupBox_scheme.Name = "TempTab_groupBox_scheme";
            this.TempTab_groupBox_scheme.Size = new System.Drawing.Size(340, 190);
            this.TempTab_groupBox_scheme.TabStop = false;
            this.TempTab_groupBox_scheme.Text = "Energy balance scheme";
            //
            // TempTab_radio_fullscheme - TEMP_V1
            //
            this.TempTab_radio_fullscheme.AutoSize = true;
            this.TempTab_radio_fullscheme.Checked = true;
            this.TempTab_radio_fullscheme.Location = new System.Drawing.Point(15, 24);
            this.TempTab_radio_fullscheme.Name = "TempTab_radio_fullscheme";
            this.TempTab_radio_fullscheme.TabStop = true;
            this.TempTab_radio_fullscheme.Text = "Full energy balance";
            this.TempTab_radio_fullscheme.UseVisualStyleBackColor = true;
            this.TempTab_radio_fullscheme.CheckedChanged += new System.EventHandler(this.TempTab_scheme_CheckedChanged);
            //
            // TempTab_radio_simplifiedscheme - TEMP_V1
            //
            this.TempTab_radio_simplifiedscheme.AutoSize = true;
            this.TempTab_radio_simplifiedscheme.Location = new System.Drawing.Point(15, 47);
            this.TempTab_radio_simplifiedscheme.Name = "TempTab_radio_simplifiedscheme";
            this.TempTab_radio_simplifiedscheme.TabStop = true;
            this.TempTab_radio_simplifiedscheme.Text = "Simplified (equilibrium temperature)";
            this.TempTab_radio_simplifiedscheme.UseVisualStyleBackColor = true;
            this.TempTab_radio_simplifiedscheme.CheckedChanged += new System.EventHandler(this.TempTab_scheme_CheckedChanged);
            //
            // TempTab_checkBox_hecraslongwave - TEMP_V1
            //
            this.TempTab_checkBox_hecraslongwave.AutoSize = true;
            this.TempTab_checkBox_hecraslongwave.Checked = true;
            this.TempTab_checkBox_hecraslongwave.CheckState = System.Windows.Forms.CheckState.Checked;
            this.TempTab_checkBox_hecraslongwave.Location = new System.Drawing.Point(15, 80);
            this.TempTab_checkBox_hecraslongwave.Name = "TempTab_checkBox_hecraslongwave";
            this.TempTab_checkBox_hecraslongwave.Text = "Use HEC-RAS default longwave (cloud-only)";
            this.TempTab_checkBox_hecraslongwave.UseVisualStyleBackColor = true;
            this.TempTab_checkBox_hecraslongwave.CheckedChanged += new System.EventHandler(this.TempTab_checkBox_hecraslongwave_CheckedChanged);
            //
            // TempTab_checkBox_hecrasalbedo - TEMP_V1
            //
            this.TempTab_checkBox_hecrasalbedo.AutoSize = true;
            this.TempTab_checkBox_hecrasalbedo.Checked = true;
            this.TempTab_checkBox_hecrasalbedo.CheckState = System.Windows.Forms.CheckState.Checked;
            this.TempTab_checkBox_hecrasalbedo.Location = new System.Drawing.Point(15, 103);
            this.TempTab_checkBox_hecrasalbedo.Name = "TempTab_checkBox_hecrasalbedo";
            this.TempTab_checkBox_hecrasalbedo.Text = "Use HEC-RAS default reflection coefficient";
            this.TempTab_checkBox_hecrasalbedo.UseVisualStyleBackColor = true;
            this.TempTab_checkBox_hecrasalbedo.CheckedChanged += new System.EventHandler(this.TempTab_checkBox_hecrasalbedo_CheckedChanged);
            //
            // TempTab_label_thermalinterval - TEMP_V1
            //
            this.TempTab_label_thermalinterval.AutoSize = true;
            this.TempTab_label_thermalinterval.Location = new System.Drawing.Point(15, 135);
            this.TempTab_label_thermalinterval.Text = "Thermal update interval (min)";
            //
            // TempTab_textBox_thermalinterval - TEMP_V1
            //
            this.TempTab_textBox_thermalinterval.Location = new System.Drawing.Point(210, 132);
            this.TempTab_textBox_thermalinterval.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_thermalinterval.Text = "60";
            this.TempTab_textBox_thermalinterval.TextChanged += new System.EventHandler(this.TempTab_textBox_thermalinterval_TextChanged);
            //
            // TempTab_label_mettimestep - TEMP_V1
            //
            this.TempTab_label_mettimestep.AutoSize = true;
            this.TempTab_label_mettimestep.Location = new System.Drawing.Point(15, 162);
            this.TempTab_label_mettimestep.Text = "Met data file time step (min)";
            //
            // TempTab_textBox_mettimestep - TEMP_V1
            //
            this.TempTab_textBox_mettimestep.Location = new System.Drawing.Point(210, 159);
            this.TempTab_textBox_mettimestep.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_mettimestep.Text = "60";
            this.TempTab_textBox_mettimestep.TextChanged += new System.EventHandler(this.TempTab_textBox_mettimestep_TextChanged);
            //
            // TempTab_groupBox_site - TEMP_V1
            //
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_label_latitude);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_textBox_latitude);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_label_longitude);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_textBox_longitude);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_label_timezone);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_textBox_timezone);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_label_elevation);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_textBox_elevation);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_label_windheight);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_textBox_windheight);
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_label_startdate); // TEMP_V1
            this.TempTab_groupBox_site.Controls.Add(this.TempTab_textBox_startdate); // TEMP_V1
            this.TempTab_groupBox_site.Location = new System.Drawing.Point(400, 220);
            this.TempTab_groupBox_site.Name = "TempTab_groupBox_site";
            this.TempTab_groupBox_site.Size = new System.Drawing.Size(340, 190);
            this.TempTab_groupBox_site.TabStop = false;
            this.TempTab_groupBox_site.Text = "Site parameters";
            //
            // TempTab_label_latitude - TEMP_V1
            //
            this.TempTab_label_latitude.AutoSize = true;
            this.TempTab_label_latitude.Location = new System.Drawing.Point(15, 24);
            this.TempTab_label_latitude.Text = "Latitude (decimal degrees, +N)";
            //
            // TempTab_textBox_latitude - TEMP_V1
            //
            this.TempTab_textBox_latitude.Location = new System.Drawing.Point(230, 21);
            this.TempTab_textBox_latitude.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_latitude.Text = "0";
            this.TempTab_textBox_latitude.TextChanged += new System.EventHandler(this.TempTab_textBox_latitude_TextChanged);
            //
            // TempTab_label_longitude - TEMP_V1
            //
            this.TempTab_label_longitude.AutoSize = true;
            this.TempTab_label_longitude.Location = new System.Drawing.Point(15, 51);
            this.TempTab_label_longitude.Text = "Longitude (decimal degrees, +E)";
            //
            // TempTab_textBox_longitude - TEMP_V1
            //
            this.TempTab_textBox_longitude.Location = new System.Drawing.Point(230, 48);
            this.TempTab_textBox_longitude.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_longitude.Text = "0";
            this.TempTab_textBox_longitude.TextChanged += new System.EventHandler(this.TempTab_textBox_longitude_TextChanged);
            //
            // TempTab_label_timezone - TEMP_V1
            //
            this.TempTab_label_timezone.AutoSize = true;
            this.TempTab_label_timezone.Location = new System.Drawing.Point(15, 78);
            this.TempTab_label_timezone.Text = "Time zone offset from UTC (hr)";
            //
            // TempTab_textBox_timezone - TEMP_V1
            //
            this.TempTab_textBox_timezone.Location = new System.Drawing.Point(230, 75);
            this.TempTab_textBox_timezone.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_timezone.Text = "0";
            this.TempTab_textBox_timezone.TextChanged += new System.EventHandler(this.TempTab_textBox_timezone_TextChanged);
            //
            // TempTab_label_elevation - TEMP_V1
            //
            this.TempTab_label_elevation.AutoSize = true;
            this.TempTab_label_elevation.Location = new System.Drawing.Point(15, 105);
            this.TempTab_label_elevation.Text = "Site elevation (m)";
            //
            // TempTab_textBox_elevation - TEMP_V1
            //
            this.TempTab_textBox_elevation.Location = new System.Drawing.Point(230, 102);
            this.TempTab_textBox_elevation.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_elevation.Text = "0";
            this.TempTab_textBox_elevation.TextChanged += new System.EventHandler(this.TempTab_textBox_elevation_TextChanged);
            //
            // TempTab_label_windheight - TEMP_V1
            //
            this.TempTab_label_windheight.AutoSize = true;
            this.TempTab_label_windheight.Location = new System.Drawing.Point(15, 132);
            this.TempTab_label_windheight.Text = "Wind measurement height (m)";
            //
            // TempTab_textBox_windheight - TEMP_V1
            //
            this.TempTab_textBox_windheight.Location = new System.Drawing.Point(230, 129);
            this.TempTab_textBox_windheight.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_windheight.Text = "10";
            this.TempTab_textBox_windheight.TextChanged += new System.EventHandler(this.TempTab_textBox_windheight_TextChanged);
            //
            // TempTab_label_startdate - TEMP_V1
            //
            this.TempTab_label_startdate.AutoSize = true;
            this.TempTab_label_startdate.Location = new System.Drawing.Point(15, 159);
            this.TempTab_label_startdate.Text = "Simulation start date/time\n(yyyy-MM-dd HH:mm)";
            //
            // TempTab_textBox_startdate - TEMP_V1
            //
            this.TempTab_textBox_startdate.Location = new System.Drawing.Point(230, 156);
            this.TempTab_textBox_startdate.Size = new System.Drawing.Size(100, 20);
            this.TempTab_textBox_startdate.Text = "2000-06-21 00:00";
            this.TempTab_textBox_startdate.TextChanged += new System.EventHandler(this.TempTab_textBox_startdate_TextChanged);
            //
            // TempTab_groupBox_initial - TEMP_V1
            //
            this.TempTab_groupBox_initial.Controls.Add(this.TempTab_label_initialtemp);
            this.TempTab_groupBox_initial.Controls.Add(this.TempTab_textBox_initialtemp);
            this.TempTab_groupBox_initial.Controls.Add(this.TempTab_label_initialraster);
            this.TempTab_groupBox_initial.Controls.Add(this.TempTab_textBox_initialraster);
            this.TempTab_groupBox_initial.Location = new System.Drawing.Point(20, 260);
            this.TempTab_groupBox_initial.Name = "TempTab_groupBox_initial";
            this.TempTab_groupBox_initial.Size = new System.Drawing.Size(340, 110);
            this.TempTab_groupBox_initial.TabStop = false;
            this.TempTab_groupBox_initial.Text = "Initial water temperature";
            //
            // TempTab_label_initialtemp - TEMP_V1
            //
            this.TempTab_label_initialtemp.AutoSize = true;
            this.TempTab_label_initialtemp.Location = new System.Drawing.Point(15, 27);
            this.TempTab_label_initialtemp.Text = "Constant value (deg C)";
            //
            // TempTab_textBox_initialtemp - TEMP_V1
            //
            this.TempTab_textBox_initialtemp.Location = new System.Drawing.Point(180, 24);
            this.TempTab_textBox_initialtemp.Size = new System.Drawing.Size(60, 20);
            this.TempTab_textBox_initialtemp.Text = "15";
            this.TempTab_textBox_initialtemp.TextChanged += new System.EventHandler(this.TempTab_textBox_initialtemp_TextChanged);
            //
            // TempTab_label_initialraster - TEMP_V1
            //
            this.TempTab_label_initialraster.AutoSize = true;
            this.TempTab_label_initialraster.Location = new System.Drawing.Point(15, 60);
            this.TempTab_label_initialraster.Text = "Initial raster file (optional, overrides constant)";
            //
            // TempTab_textBox_initialraster - TEMP_V1
            //
            this.TempTab_textBox_initialraster.Location = new System.Drawing.Point(15, 80);
            this.TempTab_textBox_initialraster.Size = new System.Drawing.Size(240, 20);
            this.TempTab_textBox_initialraster.Text = "null";
            //
            // TempTab_label_sourcetemp - TEMP_V1
            //
            this.TempTab_label_sourcetemp.AutoSize = true;
            this.TempTab_label_sourcetemp.Location = new System.Drawing.Point(20, 380);
            this.TempTab_label_sourcetemp.Name = "TempTab_label_sourcetemp";
            this.TempTab_label_sourcetemp.Text = "Source temperature file for rain/tide/reach, optional\n(see docs for column format)";
            //
            // TempTab_textBox_sourcetemp - TEMP_V1
            //
            this.TempTab_textBox_sourcetemp.Location = new System.Drawing.Point(20, 410);
            this.TempTab_textBox_sourcetemp.Name = "TempTab_textBox_sourcetemp";
            this.TempTab_textBox_sourcetemp.Size = new System.Drawing.Size(300, 20);
            this.TempTab_textBox_sourcetemp.Text = "null";
            // 
            // label107
            // 
            this.label107.AutoSize = true;
            this.label107.Enabled = false;
            this.label107.Location = new System.Drawing.Point(180, 15);
            this.label107.Name = "label107";
            this.label107.Size = new System.Drawing.Size(14, 13);
            this.label107.TabIndex = 6;
            this.label107.Text = "B";
            // 
            // label106
            // 
            this.label106.AutoSize = true;
            this.label106.Enabled = false;
            this.label106.Location = new System.Drawing.Point(126, 15);
            this.label106.Name = "label106";
            this.label106.Size = new System.Drawing.Size(15, 13);
            this.label106.TabIndex = 4;
            this.label106.Text = "G";
            // 
            // label110
            // 
            this.label110.AutoSize = true;
            this.label110.Enabled = false;
            this.label110.Location = new System.Drawing.Point(4, 13);
            this.label110.Name = "label110";
            this.label110.Size = new System.Drawing.Size(69, 13);
            this.label110.TabIndex = 0;
            this.label110.Text = "Water tracer:";
            this.label110.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.toolTip1.SetToolTip(this.label110, "Select tracer layers to use in RGB image. Rain is layer 1; Stage is layer 2; Hydr" +
        "o inputs are layers 3+.");
            // 
            // label109
            // 
            this.label109.AutoSize = true;
            this.label109.Enabled = false;
            this.label109.Location = new System.Drawing.Point(72, 15);
            this.label109.Name = "label109";
            this.label109.Size = new System.Drawing.Size(15, 13);
            this.label109.TabIndex = 2;
            this.label109.Text = "R";
            // 
            // label108
            // 
            this.label108.AutoSize = true;
            this.label108.Enabled = false;
            this.label108.Location = new System.Drawing.Point(494, 13);
            this.label108.Name = "label108";
            this.label108.Size = new System.Drawing.Size(50, 13);
            this.label108.TabIndex = 155;
            this.label108.Text = "Enhance";
            // 
            // Panel1
            // 
            this.Panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.Panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.Panel1.Controls.Add(this.tempdata2);
            this.Panel1.Controls.Add(this.tempdata1);
            this.Panel1.Controls.Add(this.graphicToGoogleEarthButton);
            this.Panel1.Controls.Add(this.button4);
            this.Panel1.Controls.Add(this.button3);
            this.Panel1.Controls.Add(this.recirculatebox);
            this.Panel1.Controls.Add(this.flowonlybox);
            this.Panel1.Location = new System.Drawing.Point(7, 493);
            this.Panel1.Name = "Panel1";
            this.Panel1.Size = new System.Drawing.Size(822, 49);
            this.Panel1.TabIndex = 145;
            this.Panel1.Visible = false;
            // 
            // tempdata2
            // 
            this.tempdata2.Location = new System.Drawing.Point(548, 7);
            this.tempdata2.Name = "tempdata2";
            this.tempdata2.Size = new System.Drawing.Size(39, 20);
            this.tempdata2.TabIndex = 145;
            this.tempdata2.Text = "358";
            // 
            // tempdata1
            // 
            this.tempdata1.Location = new System.Drawing.Point(503, 7);
            this.tempdata1.Name = "tempdata1";
            this.tempdata1.Size = new System.Drawing.Size(39, 20);
            this.tempdata1.TabIndex = 98;
            this.tempdata1.Text = "358";
            // 
            // graphicToGoogleEarthButton
            // 
            this.graphicToGoogleEarthButton.Location = new System.Drawing.Point(693, 8);
            this.graphicToGoogleEarthButton.Name = "graphicToGoogleEarthButton";
            this.graphicToGoogleEarthButton.Size = new System.Drawing.Size(119, 28);
            this.graphicToGoogleEarthButton.TabIndex = 150;
            this.graphicToGoogleEarthButton.Text = "graphic to google earth";
            this.graphicToGoogleEarthButton.UseVisualStyleBackColor = true;
            this.graphicToGoogleEarthButton.Click += new System.EventHandler(this.graphicToGoogleEarthButton_Click);
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(8, 4);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(112, 28);
            this.button4.TabIndex = 144;
            this.button4.Text = "update graphics";
            this.button4.Click += new System.EventHandler(this.button4_Click_1);
            // 
            // button3
            // 
            this.button3.Font = new System.Drawing.Font("Monotype Corsiva", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button3.Location = new System.Drawing.Point(604, 5);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(82, 29);
            this.button3.TabIndex = 99;
            this.button3.Text = "Grass now!";
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // recirculatebox
            // 
            this.recirculatebox.Location = new System.Drawing.Point(221, 6);
            this.recirculatebox.Name = "recirculatebox";
            this.recirculatebox.Size = new System.Drawing.Size(128, 24);
            this.recirculatebox.TabIndex = 89;
            this.recirculatebox.Text = "recirculate sediment";
            // 
            // flowonlybox
            // 
            this.flowonlybox.Location = new System.Drawing.Point(370, 5);
            this.flowonlybox.Name = "flowonlybox";
            this.flowonlybox.Size = new System.Drawing.Size(104, 24);
            this.flowonlybox.TabIndex = 97;
            this.flowonlybox.Text = "flow only?";
            // 
            // statusBar1
            //
            this.statusBar1.Location = new System.Drawing.Point(0, 578);
            this.statusBar1.Name = "statusBar1";
            this.statusBar1.Panels.AddRange(new System.Windows.Forms.StatusBarPanel[] {
            this.InfoStatusPanel,
            this.IterationStatusPanel,
            this.TimeStatusPanel,
            this.QwStatusPanel,
            this.QsStatusPanel,
            this.tempStatusPanel});
            this.statusBar1.ShowPanels = true;
            this.statusBar1.Size = new System.Drawing.Size(1149, 22);
            this.statusBar1.SizingGrip = false;
            this.statusBar1.TabIndex = 144;
            this.statusBar1.Text = "statusBar1";
            //
            // InfoStatusPanel
            //
            this.InfoStatusPanel.Name = "InfoStatusPanel";
            this.InfoStatusPanel.Text = "info";
            this.InfoStatusPanel.Width = 200;
            //
            // IterationStatusPanel
            //
            this.IterationStatusPanel.Name = "IterationStatusPanel";
            this.IterationStatusPanel.Text = "iterations";
            this.IterationStatusPanel.Width = 120;
            //
            // TimeStatusPanel
            //
            this.TimeStatusPanel.Name = "TimeStatusPanel";
            this.TimeStatusPanel.Text = "time";
            this.TimeStatusPanel.Width = 120;
            //
            // QwStatusPanel
            //
            this.QwStatusPanel.Name = "QwStatusPanel";
            this.QwStatusPanel.Text = "Qw";
            this.QwStatusPanel.Width = 120;
            //
            // QsStatusPanel
            //
            this.QsStatusPanel.Name = "QsStatusPanel";
            this.QsStatusPanel.Text = "Qs";
            this.QsStatusPanel.Width = 120;
            //
            // tempStatusPanel
            //
            this.tempStatusPanel.Name = "tempStatusPanel";
            this.tempStatusPanel.Text = "tempdata";
            //
            // start_button
            //
            this.start_button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.start_button.Enabled = false;
            this.start_button.Location = new System.Drawing.Point(114, 531);
            this.start_button.Name = "start_button";
            this.start_button.Size = new System.Drawing.Size(88, 25);
            this.start_button.TabIndex = 146;
            this.start_button.Text = "Start!";
            this.start_button.Click += new System.EventHandler(this.main_loop);
            //
            // button1
            //
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.button1.Enabled = false;
            this.button1.Location = new System.Drawing.Point(207, 531);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 25);
            this.button1.TabIndex = 147;
            this.button1.Text = "Quit and save";
            this.button1.Click += new System.EventHandler(this.button1_Click);
            //
            // checkBox2
            //
            checkBox2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            checkBox2.AutoSize = true;
            checkBox2.Location = new System.Drawing.Point(407, 560);
            checkBox2.Name = "checkBox2";
            checkBox2.Size = new System.Drawing.Size(108, 17);
            checkBox2.TabIndex = 151;
            checkBox2.Text = "point info window";
            checkBox2.UseVisualStyleBackColor = true;
            checkBox2.CheckedChanged += new System.EventHandler(this.checkBox2_CheckedChanged);
            //
            // checkBox1
            //
            this.checkBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(313, 539);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(77, 17);
            this.checkBox1.TabIndex = 151;
            this.checkBox1.Text = "view tabs?";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged_1);
            //
            // trackBar1
            //
            this.trackBar1.AutoSize = false;
            this.trackBar1.Location = new System.Drawing.Point(216, 9);
            this.trackBar1.Name = "trackBar1";
            this.trackBar1.Size = new System.Drawing.Size(104, 20);
            this.trackBar1.TabIndex = 149;
            this.trackBar1.Scroll += new System.EventHandler(this.trackBar1_Scroll);
            //
            // label61
            //
            this.label61.AutoSize = true;
            this.label61.Location = new System.Drawing.Point(6, 14);
            this.label61.Name = "label61";
            this.label61.Size = new System.Drawing.Size(44, 13);
            this.label61.TabIndex = 150;
            this.label61.Text = "Graphic";
            //
            // groupBox2
            //
            this.groupBox2.Controls.Add(this.label62);
            this.groupBox2.Controls.Add(this.comboBox1);
            this.groupBox2.Controls.Add(this.trackBar1);
            this.groupBox2.Controls.Add(this.label61);
            this.groupBox2.Location = new System.Drawing.Point(12, -3);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(334, 35);
            this.groupBox2.TabIndex = 151;
            this.groupBox2.TabStop = false;
            this.groupBox2.Visible = false;
            //
            // label62
            //
            this.label62.AutoSize = true;
            this.label62.Location = new System.Drawing.Point(170, 14);
            this.label62.Name = "label62";
            this.label62.Size = new System.Drawing.Size(46, 13);
            this.label62.TabIndex = 152;
            this.label62.Text = "Contrast";
            //
            // comboBox1
            //
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(54, 10);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(104, 21);
            this.comboBox1.TabIndex = 151;
            this.comboBox1.SelectedValueChanged += new System.EventHandler(this.comboBox1_SelectedValueChanged);
            //
            // groupBox3
            //
            this.groupBox3.Controls.Add(this.label63);
            this.groupBox3.Controls.Add(this.trackBar2);
            this.groupBox3.Location = new System.Drawing.Point(346, -3);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(156, 35);
            this.groupBox3.TabIndex = 152;
            this.groupBox3.TabStop = false;
            this.groupBox3.Visible = false;
            //
            // label63
            //
            this.label63.AutoSize = true;
            this.label63.Location = new System.Drawing.Point(6, 13);
            this.label63.Name = "label63";
            this.label63.Size = new System.Drawing.Size(34, 13);
            this.label63.TabIndex = 154;
            this.label63.Text = "Zoom";
            //
            // trackBar2
            //
            this.trackBar2.AutoSize = false;
            this.trackBar2.Location = new System.Drawing.Point(42, 9);
            this.trackBar2.Minimum = 1;
            this.trackBar2.Name = "trackBar2";
            this.trackBar2.Size = new System.Drawing.Size(104, 20);
            this.trackBar2.TabIndex = 153;
            this.trackBar2.Value = 5;
            this.trackBar2.Scroll += new System.EventHandler(this.trackBar2_Scroll);
            //
            // backgroundWorker1
            //
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            //
            // zoomPanImageBox1
            //
            this.zoomPanImageBox1.Image = null;
            this.zoomPanImageBox1.Location = new System.Drawing.Point(7, 37);
            this.zoomPanImageBox1.Name = "zoomPanImageBox1";
            this.zoomPanImageBox1.Size = new System.Drawing.Size(824, 316);
            this.zoomPanImageBox1.TabIndex = 148;
            this.zoomPanImageBox1.Visible = false;
            this.zoomPanImageBox1.Load += new System.EventHandler(this.zoomPanImageBox1_Load);
            //
            // groupBox9
            //
            this.groupBox9.Controls.Add(this.label108);
            this.groupBox9.Controls.Add(this.trackBar3);
            this.groupBox9.Controls.Add(this.checkBox12);
            this.groupBox9.Controls.Add(this.checkBoxSoluteVis);
            this.groupBox9.Controls.Add(this.label107);
            this.groupBox9.Controls.Add(this.comboBox4);
            this.groupBox9.Controls.Add(this.label106);
            this.groupBox9.Controls.Add(this.comboBox3);
            this.groupBox9.Controls.Add(this.label109);
            this.groupBox9.Controls.Add(this.comboBox2);
            this.groupBox9.Controls.Add(this.label110);
            this.groupBox9.Enabled = false;
            this.groupBox9.Location = new System.Drawing.Point(498, -3);
            this.groupBox9.Name = "groupBox9";
            this.groupBox9.Size = new System.Drawing.Size(550, 35);
            this.groupBox9.TabIndex = 154;
            this.groupBox9.TabStop = false;
            //
            // trackBar3
            //
            this.trackBar3.AutoSize = false;
            this.trackBar3.Enabled = false;
            this.trackBar3.Location = new System.Drawing.Point(390, 9);
            this.trackBar3.Minimum = 1;
            this.trackBar3.Name = "trackBar3";
            this.trackBar3.Size = new System.Drawing.Size(104, 20);
            this.trackBar3.TabIndex = 154;
            this.trackBar3.Value = 5;
            this.trackBar3.Scroll += new System.EventHandler(this.trackBar3_Scroll);
            //
            // checkBox12
            //
            this.checkBox12.AutoSize = true;
            this.checkBox12.Enabled = false;
            this.checkBox12.Location = new System.Drawing.Point(246, 14);
            this.checkBox12.Name = "checkBox12";
            this.checkBox12.Size = new System.Drawing.Size(79, 17);
            this.checkBox12.TabIndex = 7;
            this.checkBox12.Text = "Rain zones";
            this.checkBox12.UseVisualStyleBackColor = true;
            this.checkBox12.CheckedChanged += new System.EventHandler(this.checkBox12_CheckedChanged);
            //
            // checkBoxSoluteVis
            //
            this.checkBoxSoluteVis.AutoSize = true;
            this.checkBoxSoluteVis.Enabled = false;
            this.checkBoxSoluteVis.Location = new System.Drawing.Point(326, 14);
            this.checkBoxSoluteVis.Name = "checkBoxSoluteVis";
            this.checkBoxSoluteVis.Size = new System.Drawing.Size(61, 17);
            this.checkBoxSoluteVis.TabIndex = 156;
            this.checkBoxSoluteVis.Text = "Solutes";
            this.checkBoxSoluteVis.UseVisualStyleBackColor = true;
            this.checkBoxSoluteVis.CheckedChanged += new System.EventHandler(this.checkBoxSoluteVis_CheckedChanged);
            //
            // comboBox4
            //
            this.comboBox4.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox4.DropDownWidth = 140;
            this.comboBox4.Enabled = false;
            this.comboBox4.FormattingEnabled = true;
            this.comboBox4.Location = new System.Drawing.Point(197, 10);
            this.comboBox4.Name = "comboBox4";
            this.comboBox4.Size = new System.Drawing.Size(34, 21);
            this.comboBox4.Sorted = true;
            this.comboBox4.TabIndex = 5;
            this.comboBox4.SelectedValueChanged += new System.EventHandler(this.comboBox4_SelectedValueChanged);
            //
            // comboBox3
            //
            this.comboBox3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox3.DropDownWidth = 140;
            this.comboBox3.Enabled = false;
            this.comboBox3.FormattingEnabled = true;
            this.comboBox3.Location = new System.Drawing.Point(143, 10);
            this.comboBox3.Name = "comboBox3";
            this.comboBox3.Size = new System.Drawing.Size(34, 21);
            this.comboBox3.Sorted = true;
            this.comboBox3.TabIndex = 3;
            this.comboBox3.SelectedValueChanged += new System.EventHandler(this.comboBox3_SelectedValueChanged);
            //
            // comboBox2
            //
            this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox2.DropDownWidth = 140;
            this.comboBox2.Enabled = false;
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Location = new System.Drawing.Point(89, 10);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(34, 21);
            this.comboBox2.Sorted = true;
            this.comboBox2.TabIndex = 1;
            this.comboBox2.SelectedValueChanged += new System.EventHandler(this.comboBox2_SelectedValueChanged);
            //
            // Form1
            //
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.AutoScroll = true;
            this.ClientSize = new System.Drawing.Size(1149, 600);
            this.Controls.Add(checkBox2);
            this.Controls.Add(this.checkBox1);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.start_button);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.statusBar1);
            this.Controls.Add(this.Panel1);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.zoomPanImageBox1);
            this.Controls.Add(this.groupBox9);
            this.Menu = this.mainMenu1;
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CAESAR Lisflood 2.0";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.tabControl1.ResumeLayout(false);
            this.FilesTab.ResumeLayout(false);
            this.FilesTab.PerformLayout();
            this.groupBox6.ResumeLayout(false);
            this.groupBox6.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox4.PerformLayout();
            this.groupBoxOutputDirectory.ResumeLayout(false);
            this.groupBoxOutputDirectory.PerformLayout();
            this.NumericalTab.ResumeLayout(false);
            this.NumericalTab.PerformLayout();
            this.GrainTab.ResumeLayout(false);
            this.GrainTab.PerformLayout();
            this.DescriptionTab.ResumeLayout(false);
            this.DescriptionTab.PerformLayout();
            this.GridTab.ResumeLayout(false);
            this.GridTab.PerformLayout();
            this.HydrologyTab.ResumeLayout(false);
            this.groupBox7.ResumeLayout(false);
            this.groupBox7.PerformLayout();
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBoxWaterSourceTracer.ResumeLayout(false);
            this.groupBoxWaterSourceTracer.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.groupBox8.ResumeLayout(false);
            this.groupBox8.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.OilTab.ResumeLayout(false);
            this.OilTab.PerformLayout();
            this.OilTab_groupBox_controls.ResumeLayout(false);
            this.OilTab_groupBox_controls.PerformLayout();
            this.Panel1.ResumeLayout(false);
            this.Panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.InfoStatusPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.IterationStatusPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.TimeStatusPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.QwStatusPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.QsStatusPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tempStatusPanel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar1)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar2)).EndInit();
            this.groupBox9.ResumeLayout(false);
            this.groupBox9.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trackBar3)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.Run(new Form1());
        }

        private void start_button_Click(object sender, System.EventArgs e)
        {
            xtextbox.Visible = false;
            ytextbox.Visible = false;
            button1.Visible = false;
            button2.Visible = false;
            label1.Visible = false;
            label2.Visible = false;

        }


        private void main_loop(object sender, System.EventArgs e)
        {


            /********* locals ***********/
            double tempflow = baseflow;
            double ince = cycle + 60;

            gameClock = new System.Windows.Forms.Timer();

            //			clearforms();                  // MJ 14/01/05   this is done when load button is clicked
            start_button.Enabled = false;  // MJ 17/01/05
            button1.Enabled = true;        // MJ 17/01/05
            comboBox1.Items.Add("water depth");
            popComboBox1();

            time_1 = 1;

            calc_J(1.0);

            save_time = cycle;
            creep_time = cycle;
            creep_time2 = cycle;
            soil_erosion_time = cycle;
            soil_development_time = cycle;
            time_1 = cycle;

            mygraphics = this.CreateGraphics();


            //slide_5();
            get_area();

            get_catchment_input_points();

            this.InfoStatusPanel.Text = "Starting main loop";        // MJ 14/01/05
                                                                     //JMW

            this.InfoStatusPanel.Text = "Running";        // MJ 14/01/05

            // MDW_V2 set/ create output directory
            outDirCheck();

            //Googel Earth Animation Directory
            if (googleAnimationCheckbox.Checked)
            {
                // MDW_V2: added outdir to path: defaults to empty (i.e., no change), or will create subdirectory in user specified output directory
                if (string.IsNullOrEmpty(outdir) == false)
                {
                    KML_FILE_NAME = Path.Combine(outdir, KML_FILE_NAME);
                    googleAnimationDir = Path.Combine(outdir, googleAnimationDir);
                }
                if (Directory.Exists(googleAnimationDir)) Directory.Delete(googleAnimationDir, true);
                Directory.CreateDirectory(googleAnimationDir);
            }

            time_factor = 1;

            // Here we go into the main loop,
            // I copied this bit of code to get
            // the event handler to work - and it seems OK so why change it.
            // so the main loop is really in erodedepo()

            gameClock.Interval = 100;
            gameClock.Tick += new EventHandler(update_screen_outputs);
            gameClock.Start();

            backgroundWorker1.RunWorkerAsync();

        }

        private void erodedepo() // erodedepo is the main loop from which all the main CL functions are called.
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            int y;
            do
            {

                // set of lines commented out to instigate uplift at set times (if needed)
                // line to do uplift at set time....
                //if (cycle > (1440 * 18250) && elev[1, 1] < 100)
                //{
                //    for (int x = 1; x <= 300; x++)
                //    {
                //        for (y = 1; y <= ymax; y++)
                //        {
                //            elev[x, y] += 10;//Convert.ToInt32(textBox6.Text);
                //        }
                //    }
                //    slide_5();
                //    slide_5();
                //    elev[1, 1] = 100;
                //}

                // Gez code: set previous cycle = to cycle
                previous = cycle;
                old_cycle = cycle % output_file_save_interval;
                // end gez code

                //
                // section below workign out time step.
                //
                double input_output_difference = Math.Abs(waterinput - waterOut);
                //this.tempdata1.Text = Convert.ToString(waterinput-waterOut);

                // all dealing with calculating model time step
                if (maxdepth <= water_depth_erosion_threshold) maxdepth = water_depth_erosion_threshold;
                //if (time_factor < min_time_step) time_factor = min_time_step;
                if (time_factor < (courant_number * (DX / Math.Sqrt(gravity * (maxdepth))))) time_factor = (courant_number * (DX / Math.Sqrt(gravity * (maxdepth))));
                if (input_output_difference > in_out_difference && time_factor > (courant_number * (DX / Math.Sqrt(gravity * (maxdepth)))))
                {
                    time_factor = courant_number * (DX / Math.Sqrt(gravity * (maxdepth)));
                }

                double local_time_factor = time_factor;
                if (local_time_factor > (courant_number * (DX / Math.Sqrt(gravity * (maxdepth))))) local_time_factor = courant_number * (DX / Math.Sqrt(gravity * (maxdepth)));



                // code to incrememtn counters. Counter is model iterations, cycle is the actual (modelled reality) time
                counter++;
                cycle += time_factor / 60;
                // cycle in min
                // time_factor in secs.

                // Gez code
                new_cycle = cycle % output_file_save_interval;
                // end gez code


                //
                // adding in sand movement gradually
                if (DuneBox.Checked == true && cycle >= duneupdatetime)
                {
                    double factor = (max_time_step / 60) / double.Parse(dune_time_box.Text);
                    //factor = 1;
                    for (int x = 1; x <= xmax; x++)
                    {
                        for (y = 1; y <= ymax; y++)
                        {
                            if (elev_diff[x, y] != 0)
                            {
                                if (elev[x, y] < init_elevs[x, y] && elev_diff[x, y] > 0) elev_diff[x, y] = 0;
                                elev[x, y] -= elev_diff[x, y] * factor;
                                //elev_diff[x, y] = 0;
                                if (index[x, y] != -9999 && elev_diff[x, y] < 0 && water_depth[x, y] >= water_depth_erosion_threshold)
                                {
                                    for (int T = 0; T <= tracers; T++) grain[index[x, y], 1, T] -= elev_diff[x, y] * factor;
                                    sand[x, y] += elev_diff[x, y] * factor;
                                    sort_active(x, y);
                                }
                            }
                        }
                    }
                    duneupdatetime = cycle + (max_time_step / 60);
                }


                ////			This whole section below deals with the water inputs. A separate part for point inputs and
                ////		    a different bit for gradual inputs from the whole of the catchment. (reach or catchment)
                // first zero counter to tally up water inputs
                waterinput = 0;

                // reach mode water inputs
                if (reach_mode_box.Checked == true) reach_water_and_sediment_input(local_time_factor);

                // catchment mode water inputs
                if (catchment_mode_box.Checked == true) catchment_water_input_and_hydrology(local_time_factor);

                // tidal/stage mode water inputs
                if (checkBox3.Checked == true) stage_tidal_input(local_time_factor);

                // save water tracer states
                if (isTraceWater == true) save_tracer_states();
                if (isSimulateTemperature == true) save_temperature_states(); // TEMP_V1

                // route water and update flow depths

                qroute();
                depth_update();

                // update water tracers
                if (isTraceWater == true) update_tracer_states();

                // TEMP_V1 - fine-cadence temperature advection/mixing, every hydraulic iteration
                if (isSimulateTemperature == true) update_water_temperature_advection(local_time_factor);

                // update oil state
                if (isOilSimulation == true)
                {
                    // check triggering of event
                    if (cycle > oil_startt && oil_check_depth_threshold() == true)
                    {
                        oil_eventtriggered = true;
                    }

                    if (oil_eventtriggered == true)
                    {
                        oil_spill_input(local_time_factor);
                        oil_area();
                        oil_evaporation(local_time_factor);
                        oilroute(local_time_factor);
                        oil_update();
                        oil_global_mass_balance();

                    }

                }

                // check scan area every 5 iters.. maybe re-visit for reach mode if it causes too much backing up of sed. see code commented below nex if..
                if (Math.IEEERemainder(counter, 5) == 0)
                {
                    scan_area();
                }

                // carry out erosion

                if (!flowonlybox.Checked)
                {
                    if (counter >= erode_call)
                    {
                        erode_mult = (int)(ERODEFACTOR / erode(erode_mult));
                        if (erode_mult < 1) erode_mult = 1;
                        if (erode_mult > 10) erode_mult = 10;

                        erode_call = counter + erode_mult;
                        //this.tempdata1.Text = Convert.ToString(erode_mult);
                    }
                    //
                    // carry out lateral erosion
                    //
                    if (newlateral.Checked && counter >= lateralcounter)
                    {
                        lateral3();
                        lateralcounter = counter + (50 * erode_mult);
                    }
                }

                //
                // work out water coming out..
                // and zero water depths in edges..
                //
                // this doesnt actually zero water - sets it to the min water depth
                // If this is not done, then zero'd water depth prevents material being moved to the edge cells and thus eroded fromthe catchment
                // leading to buildups of sediment at the edge.

                double temptot = 0;
                for (y = 1; y <= ymax; y++)
                {
                    if (water_depth[xmax, y] > water_depth_erosion_threshold)
                    {
                        temptot += (water_depth[xmax, y] - water_depth_erosion_threshold) * DX * DX / local_time_factor;
                        // and zero water depths at RH end
                        water_depth[xmax, y] = water_depth_erosion_threshold;
                    }
                    if (water_depth[1, y] > water_depth_erosion_threshold)
                    {
                        temptot += (water_depth[1, y] - water_depth_erosion_threshold) * DX * DX / local_time_factor;
                        // and zero water depths at RH end
                        water_depth[1, y] = water_depth_erosion_threshold;
                    }
                }
                for (int x = 1; x <= xmax; x++)
                {
                    if (water_depth[x, 1] > water_depth_erosion_threshold)
                    {
                        temptot += (water_depth[x, 1] - water_depth_erosion_threshold) * DX * DX / local_time_factor;
                        // and zero water depths at RH end
                        water_depth[x, 1] = water_depth_erosion_threshold;
                    }
                    if (water_depth[x, ymax] > water_depth_erosion_threshold)
                    {
                        temptot += (water_depth[x, ymax] - water_depth_erosion_threshold) * DX * DX / local_time_factor;
                        // and zero water depths at RH end
                        water_depth[x, ymax] = water_depth_erosion_threshold;
                    }
                }
                waterOut = temptot;


                if (isOilSimulation == true)
                {
                    Vout_total = 0;
                    for (y = 1; y <= ymax; y++)
                    {
                        if (oil_depth[xmax, y] > 0)
                        {
                            Vout_total += oil_depth[xmax, y] * DX * DX;
                            oil_depth[xmax, y] = 0;

                        }
                        if (oil_depth[1, y] > 0)
                        {
                            Vout_total += oil_depth[1, y] * DX * DX;
                            oil_depth[1, y] = 0;
                        }

                        // Condition to avoid the oil accumulation at the edges


                    }

                    // Condition added to avoid the accumulation on the edge, a bit crude but it works. 
                    for (int x = 1; x <= xmax; x++)
                    {
                        if (oil_depth[x, ymax] > 0)
                        {
                            // Oil out of the edges
                            Vout_total += oil_depth[x, ymax] * DX * DX;
                            oil_depth[x, ymax] = 0;

                        }
                        if (oil_depth[x, 1] > 0)
                        {
                            Vout_total += oil_depth[x, 1] * DX * DX;
                            oil_depth[x, 1] = 0;
                        }

                    }
                    // Vout += Vout_1 + Vout_2 + Vout_3 + Vout_4;


                }






                // then carry out soil creep.
                //- also growing grass...
                // and dunes...
                // slide_3 is looking at landslides only in the 'scanned' area
                // slide_4 is to do with dunes
                // slide_5 is landslides everywhere..

                if (!flowonlybox.Checked)
                {
                    // carry out local landslides every X iterations...
                    if (Math.IEEERemainder(counter, 10) == 0)
                    {
                        slide_3();
                    }
                    // soil creep - here every 10 days
                    if (cycle > creep_time)
                    {
                        // updating counter creep_time
                        creep_time += 14400;
                        // now calling soil creep function
                        creep(0.028);//(0.019);//(0.083);
                    }
                    //// doing sand dunes part

                    if (DuneBox.Checked == true && cycle > dune_time)
                    {
                        dune1(0);
                        dune_time += double.Parse(dune_time_box.Text); ;
                    }

                    //
                    // now calling soil erosion function  - also checks if soil erosion rate is > 0
                    // to prevent it calling it if not required.
                    // then soil erosion time
                    //

                    if (SOIL_RATE > 0 && cycle > soil_erosion_time)
                    {
                        get_area(); // gets drainage area before doing soil erosion - as used in the calcs. therefore good to have accurate/fresh drianage area
                        soil_erosion_time += 1440;// do soil erosion daily
                        //if (soilerosionBox.Checked == true) SOIL_RATE = ((0.768383841078 * j_mean + 0.000001979564));
                        soilerosion(0.0028);
                    }

                    if (cycle > soil_development_time)
                    {
                        soil_development_time += 1440 * 365 / 12;
                        if (soildevbox.Checked) soil_development();
                    }

                }



                // other slope things.. done every day.
                if (cycle > creep_time2)
                {
                    evaporate(1440);
                    // updating counter creep_time2
                    creep_time2 += 1440;// daily

                    if (!flowonlybox.Checked)
                    {

                        slide_5();

                        // calling siberia model (if wanted)
                        if (SiberiaBox.Checked == true)
                        {
                            get_area();
                            siberia(0.002739); // the value passed is the time step (in years)
                        }
                        // next line does grass growing, must change it if changes from monthly update
                        if (grow_grass_time > 0) grow_grass(1 / (grow_grass_time * 365));

                        // check for adding mine inputs
                        if (this.mine_input_textBox.Text != "null") add_minewaste();

                    }

                }

                // TEMP_V1 - coarse-cadence surface energy balance, checked every iteration,
                // independent of creep_time2's daily schedule
                if (isSimulateTemperature == true && cycle > thermal_time)
                {
                    update_water_temperature_energybalance();
                    thermal_time += thermal_update_interval;
                }

                // Gez
                temptotal = temptot;

                // call output routine
                // only if iteration by iteration output is required.
                if (checkBoxGenerateTimeSeries.Checked)  // MJ 20/01/05
                {
                    output_data();
                }
                // end gez code

                //Google Earth Animation outputs data to file.
                if (cycle >= save_time2 && googleAnimationCheckbox.Checked)
                {
                    googleoutputflag = true;
                    save_time2 += save_interval2;
                    imageCount2 = imageCount2 + 1;
                }

                // save data & draw graphics every specified interval. Passes the cycle whuch becomes the file extension
                if (cycle >= save_time && uniquefilecheck.Checked == true)
                {
                    save_data_and_draw_graphics();
                    save_time += saveinterval;
                }
                // if its at the end of the run kill the program
            } while (cycle / 60 < maxcycle);

            watch.Stop();
            MessageBox.Show($"finished. Runtime: {watch.Elapsed} ");
            // end of main loop!!
        }


        void catchment_water_input_and_hydrology(double local_time_factor)
        {
            // revised way of determining water input 21/8/18
            double tempwaterinput = 0;

            for (int z = 1; z <= totalinputpoints; z++)
            {
                int x = catchment_input_x_coord[z];
                int y = catchment_input_y_coord[z];
                int zoneInd = -1; //MDW
                double h_from_this_source = 0.0, prev_depth = 0.0, raindepth; // MDW
                double water_add_amt = (j_mean[rfarea[x, y]] * nActualGridCells[rfarea[x, y]]) / (catchment_input_counter[rfarea[x, y]]) * local_time_factor;
                // revised way of getting water input 21/8/18
                tempwaterinput += water_add_amt * DX * DX / local_time_factor;
                if (water_add_amt > ERODEFACTOR) water_add_amt = ERODEFACTOR;

                prev_depth = water_depth[x, y];
                water_depth[x, y] += water_add_amt;

                // TEMP_V1 - assign/mix temperature of rainfall input water.
                if (isSimulateTemperature == true)
                {
                    double sourceTemp_rain = get_source_temperature(1, cycle); // TEMP_V1 - rain is always source index 1
                    if (prev_depth <= 0.0 || water_temp[x, y] == -9999)
                    {
                        water_temp[x, y] = sourceTemp_rain;
                    }
                    else
                    {
                        water_temp[x, y] = ((water_temp[x, y] * prev_depth) + (sourceTemp_rain * water_add_amt)) / water_depth[x, y];
                    }
                }

                // Adjust water proportions following the addition of rainfall - MDW 13/03/16
                if ((isTraceWater == true) && (water_depth[x, y] > 0.0))
                {
                    if (isTraceRainZonation == true) zoneInd = Array.IndexOf(rainZones, rainzonation[x, y]); // will return -1 for cells without a zone

                    if (prev_depth == 0.0) // if depth was zero, all water in this cell is from this input
                    {
                        watertracer[x, y, 1] = 1.0; // rain input is assigned to the first layer

                        // MDW_Apr24: reset other tracers to zero here: avoiding issues of drying cells causing tracersum > 1
                        for (int src = 0; src < nSources; src++)
                        {
                            if (src != 1) watertracer[x, y, src] = 0.0;
                        }

                        if (isTraceRainZonation == true && zoneInd != -1) watertracerRainZone[x, y, zoneInd] = 1.0;
                    }
                    else if (watertracer[x, y, 1] < 1.0) // if all water is already from this source, no need to update
                    {
                        h_from_this_source = watertracer[x, y, 1] * prev_depth; // amount of water from this source already in cell

                        // update this layer water proportion - i.e. increase
                        watertracer[x, y, 1] = (h_from_this_source + water_add_amt) / water_depth[x, y];

                        // update other layers water proportions - i.e. reduce
                        // MDW_V2 updated as sources now zero-indexed; stage has moved from 2 to 0, rain stays in 1, others are 2+
                        for (int src = 0; src < nSources; src++)
                        {
                            if (src != 1) // skip rainfall source layer
                            {
                                watertracer[x, y, src] = (watertracer[x, y, src] * prev_depth) / water_depth[x, y];
                            }
                        }

                        // MDW_DEBUG
                        /* double tracersum = 0.0;
                        for (int src = 0; src <= nSources; src++)
                        {
                            tracersum += watertracer[x, y, src];
                            if (tracersum > 1.0000001)
                            {
                                MessageBox.Show("Tracer exception caught: tracersum = " + Convert.ToString(tracersum) +
                                    ", counter = " + Convert.ToString(counter) + ", cycle = " + Convert.ToString(cycle));
                            }
                        } */

                        // deal with rainfall zones - MDW 01/04/16
                        if (isTraceRainZonation == true && zoneInd != -1 && watertracerRainZone[x, y, zoneInd] < 1.0)
                        {
                            // pppp get depth of rain water in cell then use in place of prev_depth/ water_depth
                            //raindepth_prev = prev_depth * watertracer_prev[x, y, 1];
                            raindepth = water_depth[x, y] * watertracer[x, y, 1];
                            //h_from_this_source = watertracerRainZone[x, y, zoneInd] * raindepth_prev;
                            if (raindepth > 0.0) watertracerRainZone[x, y, zoneInd] = ((h_from_this_source * watertracerRainZone[x, y, zoneInd]) + water_add_amt) / raindepth;
                            else watertracerRainZone[x, y, zoneInd] = 0;
                            for (int src = 0; src < nRainZones; src++) // MDW_V2 updated to zero-index
                            {
                                if (src == zoneInd) continue;
                                if (raindepth > 0.0) watertracerRainZone[x, y, src] = (h_from_this_source * watertracerRainZone[x, y, src]) / raindepth;
                                else watertracerRainZone[x, y, src] = 0;
                            }

                            // MDW_DEBUG
                            /* tracersum = 0.0;
                            for (int src = 0; src <= nRainZones; src++)
                            {
                                tracersum += watertracerRainZone[x, y, src];
                                if (tracersum > 1.0000001)
                                {
                                    MessageBox.Show("Tracer exception caught: tracersum = " + Convert.ToString(tracersum) +
                                        ", counter = " + Convert.ToString(counter) + ", cycle = " + Convert.ToString(cycle) );
                                }
                            } */
                        }
                    }
                }

                // MDW_V2 adjust solute concentrations given rainfall input
                if ((isTraceSolutes == true) && (water_depth[x, y] > 0.0))
                {
                    for (int solute = 0; solute < nSolutes; solute++)
                    {
                        if (solutetracer[x, y, solute] > 0 && water_add_amt > 0) // if nothing is in the cell, and there's no input, skip
                        {
                            // update solute concentration in cell: dilution with rainfall
                            solutetracer[x, y, solute] = prev_depth * solutetracer[x, y, solute] / water_depth[x, y]; // proportion of depth in cell which is old * solute existing
                        }
                    }
                }

                //if ((j_mean * nActualGridCells) / (catchment_input_counter) * local_time_factor > ERODEFACTOR) this.tempStatusPanel.Text = "C add= " + Convert.ToString((j_mean * nActualGridCells) / (catchment_input_counter) * local_time_factor);
            }

            waterinput = tempwaterinput; // MDW_V2 - why is water input reset here?

            // if the input type flag is 1 then the discharge is input from the hydrograph...
            if (cycle >= time_1)
            {
                do
                {
                    time_1++;
                    calc_J(time_1);

                    if (time_factor > max_time_step)
                    {

                        double j_mean_max_temp = 0;
                        for (int n = 1; n <= rfnum; n++) if (new_j_mean[n] > j_mean_max_temp) j_mean_max_temp = new_j_mean[n];
                        if (j_mean_max_temp > (0.2 / (xmax * ymax * DX * DX)))
                        // check after variable rainfall area has been added...
                        //stops code going too fast when there is actual flow in the channels greater than 0.2cu.
                        {
                            cycle = time_1 + (max_time_step / 60);
                            time_factor = max_time_step;
                        }
                    }
                } while (time_1 < cycle);
            }

            calchydrograph(time_1 - cycle);

            double jmeanmax = 0;
            for (int n = 1; n <= rfnum; n++) if (j_mean[n] > jmeanmax) jmeanmax = j_mean[n];

            if (jmeaninputfilebox.Checked == true) j_mean[1] = ((hourly_rain_data[(int)(cycle / rain_data_time_step), 1]) / Math.Pow(DX, 2)) / nActualGridCells[1];

            if (jmeanmax >= baseflow)
            {
                baseflow = baseflow * 3;
                //Console.Write("going up.. \n");
                get_area();
                get_catchment_input_points();
            }


            if (baseflow > (jmeanmax * 3) && baseflow > 0.000001)
            {
                baseflow = jmeanmax * 1.25;
                //Console.Write("going down.. \n");
                get_area();
                get_catchment_input_points();
            }


        }

        void reach_water_and_sediment_input(double local_time_factor)
        {
            double[] remove_from_temp_grain;

            remove_from_temp_grain = new Double[G_MAX + 2];

            if (reach_mode_box.Checked == true)
            {
                for (int n = 0; n <= number_of_points - 1; n++)
                {
                    int tempn;

                    int x = inpoints[n, 0];
                    int y = inpoints[n, 1];



                    double adding_factor = 1;
                    // tot up total to be added - and if its greater than number of cells and erode_limit
                    // then reduce what can be added via a factor.
                    double added_tot = 0;

                    if (water_depth[x, y] > water_depth_erosion_threshold)
                    {
                        for (tempn = 5; tempn <= G_MAX + 3; tempn++)
                        {
                            added_tot += (Math.Abs(inputfile[n, (int)(cycle / input_time_step), tempn])) / div_inputs / (DX * DX) / (input_time_step * 60) * time_factor;
                        }

                        // then multiply by the recirculation factor..
                        if (added_tot / number_of_points > ERODEFACTOR * 0.75) adding_factor = (ERODEFACTOR * 0.75) / (added_tot / number_of_points);

                        if (index[x, y] == -9999) addGS(x, y);
                        for (tempn = 5; tempn <= G_MAX + 3; tempn++)
                        {
                            double amount_to_add = adding_factor * (Math.Abs(inputfile[n, (int)(cycle / input_time_step), tempn])) / div_inputs / (DX * DX) / (input_time_step * 60) * time_factor;
                            if (isSuspended[tempn - 4])
                            {
                                Vsusptot[x, y, 0] += amount_to_add;
                            }
                            else
                            {
                                grain[index[x, y], tempn - 4, 0] += amount_to_add;
                                elev[x, y] += amount_to_add;
                            }

                        }
                    }
                }
            }

            // add in recirculating sediment.
            if (recirculatebox.Checked == true && reach_mode_box.Checked == true)
            {
                int tempn;
                double adding_factor = 1;
                // tot up total to be added - and if its greater than number of cells and erode_limit
                // then reduce what can be added via a factor.
                double added_tot = 0;

                for (tempn = 5; tempn <= G_MAX + 3; tempn++)
                {
                    added_tot += temp_grain[tempn - 4];
                    remove_from_temp_grain[tempn - 4] = 0; // quick way of emptying this variable
                }

                // then multiply by the recirculation factor..
                if (added_tot / number_of_points > ERODEFACTOR * 0.75) adding_factor = (ERODEFACTOR * 0.75) / (added_tot / number_of_points);


                for (int n = 0; n <= number_of_points - 1; n++)
                {
                    int x = inpoints[n, 0];
                    int y = inpoints[n, 1];

                    if (water_depth[x, y] > water_depth_erosion_threshold)
                    {

                        for (tempn = 5; tempn <= G_MAX + 3; tempn++)
                        {
                            if (index[x, y] == -9999) addGS(x, y);
                            if (isSuspended[tempn - 4])
                            {
                                Vsusptot[x, y, 0] += ((temp_grain[tempn - 4] * adding_factor) / number_of_points);
                                remove_from_temp_grain[tempn - 4] += ((temp_grain[tempn - 4] * adding_factor) / number_of_points);
                            }
                            else
                            {
                                grain[index[x, y], tempn - 4, 0] += ((temp_grain[tempn - 4] * adding_factor) / number_of_points);
                                elev[x, y] += ((temp_grain[tempn - 4] * adding_factor) / number_of_points);
                                remove_from_temp_grain[tempn - 4] += ((temp_grain[tempn - 4] * adding_factor) / number_of_points);
                            }

                        }
                    }
                }

            }

            for (int n = 1; n <= G_MAX; n++)
            {
                temp_grain[n] -= remove_from_temp_grain[n];
            }



            for (int n = 0; n <= number_of_points - 1; n++)
            {
                int x = inpoints[n, 0];
                int y = inpoints[n, 1];
                double interpolated_input1 = inputfile[n, (int)(cycle / input_time_step), 1];
                double interpolated_input2 = inputfile[n, (int)(cycle / input_time_step) + 1, 1];
                double proportion_between_time1and2 = ((((int)(cycle / input_time_step) + 1) * input_time_step) - cycle)
                    / input_time_step;

                double input = interpolated_input1 + ((interpolated_input2 - interpolated_input1) * (1 - proportion_between_time1and2));
                double dhdt = 0.0, h_from_this_source = 0.0, prev_depth = 0.0; // MDW - needed for working out new water tracing propotions
                //j_mean = old_j_mean + (((new_j_mean - old_j_mean) / 2) * (2 - time));

                waterinput += (input / div_inputs);

                // trial adding SS line
                // if(counter<500)Vsusptot[x+5, y] = 0.1;
                prev_depth = water_depth[x, y];
                dhdt = (input / div_inputs) / (DX * DX) * local_time_factor; //MDW
                water_depth[x, y] += dhdt; //(input / div_inputs) / (DX * DX) * local_time_factor; //MDW_V2 avoiding double-computation of dhdt
                // also have to add suspended sediment here..
                // from file

                // TEMP_V1 - assign/mix temperature of reach/point input water.
                if (isSimulateTemperature == true)
                {
                    double sourceTemp_reach = get_source_temperature(sourceIDs[n], cycle); // TEMP_V1
                    if (prev_depth <= 0.0 || water_temp[x, y] == -9999)
                    {
                        water_temp[x, y] = sourceTemp_reach;
                    }
                    else
                    {
                        water_temp[x, y] = ((water_temp[x, y] * prev_depth) + (sourceTemp_reach * dhdt)) / water_depth[x, y];
                    }
                }

                if ((isTraceWater == true) && (water_depth[x, y] > 0.0))
                {
                    int thissrc = sourceIDs[n]; //sourceIDs[n + 1] + 2; // identify the source of this input // MDW_V2 changed to zero-index; indices changed for tide/rain

                    if (water_depth[x, y] == dhdt) // if depth was zero, all water in this cell is from this input
                    {
                        watertracer[x, y, thissrc] = 1.0; // this source layer

                        // MDW_Apr24: reset other tracers to zero here: to avoid possibility of drying cells causing tracersum > 1 (e.g. checkerboard errors?)
                        for (int src = 0; src < nSources; src++)
                        {
                            if (src != thissrc) watertracer[x, y, src] = 0.0;
                        }

                    }
                    else if (watertracer[x, y, thissrc] < 1.0) // if all water is already from this source, no need to update
                    {
                        h_from_this_source = watertracer[x, y, thissrc] * prev_depth; // amount of water from this source already in cell

                        // update this layer water proportion - i.e. increase
                        watertracer[x, y, thissrc] = (h_from_this_source + dhdt) / water_depth[x, y];

                        // update other layers water proportions - i.e. reduce
                        for (int src = 0; src < nSources; src++) // MDW_V2 updated to zero index
                        {
                            if (src != thissrc) // skip this source layer
                            {
                                watertracer[x, y, src] = (watertracer[x, y, src] * prev_depth) / water_depth[x, y];
                            }
                        }

                        // MDW_DEBUG
                        // check for error
                        /* double tracersum = 0.0;
                        for (int src = 0; src < nSources; src++)
                        {
                            tracersum += watertracer[x, y, src];
                            if (tracersum > 1.0000001)
                            {
                                MessageBox.Show("Tracer exception caught: tracersum = " + Convert.ToString(tracersum) +
                                    ", counter = " + Convert.ToString(counter) + ", cycle = " + Convert.ToString(cycle));
                            }
                        } */

                    }

                    // MDW_V2 add solutes from input files
                    if ((isTraceSolutes == true) && (water_depth[x, y] > 0.0))
                    {
                        for (int solute = 0; solute < nSolutes; solute++)
                        {
                            // get solute concentration to be added from file
                            interpolated_input1 = inputfile[n, (int)(cycle / input_time_step), solute + 14]; // solutes are in the 15th column onwards (zero based index)
                            interpolated_input2 = inputfile[n, (int)(cycle / input_time_step) + 1, solute + 14];
                            input = interpolated_input1 + ((interpolated_input2 - interpolated_input1) * (1 - proportion_between_time1and2));

                            if (input > 0)
                            {
                                // update solute concentration in cell, as a depth-weighted average of existing and new solute concentrations
                                solutetracer[x, y, solute] =
                                    ((dhdt * input) + // depth in cell which is new * solute added
                                    (prev_depth * solutetracer_prev[x, y, solute]))   // depth in cell which is old * solute existing
                                    / water_depth[x, y];
                            }
                        }


                    }



                }


            }



        }

        void stage_tidal_input(double local_time_factor)
        {
            for (int x = Math.Min(fromx, tox); x <= Math.Max(fromx, tox); x++)
            {
                for (int y = Math.Min(fromy, toy); y <= Math.Max(fromy, toy); y++)
                {
                    double interpolated_input1 = stage_inputfile[(int)(cycle / stage_input_time_step)];
                    double interpolated_input2 = stage_inputfile[(int)(cycle / stage_input_time_step) + 1];
                    double proportion_between_time1and2 = ((((int)(cycle / stage_input_time_step) + 1) * stage_input_time_step) - cycle)
                        / stage_input_time_step;
                    double dhdt = 0.0, h_from_this_source = 0.0, prev_depth = 0.0; // Needed for water proportions tracing - MDW

                    double input = interpolated_input1 + ((interpolated_input2 - interpolated_input1) * (1 - proportion_between_time1and2));

                    if (elev[x, y] > -9999 && input > elev[x, y])
                    {
                        prev_depth = water_depth[x, y];
                        dhdt = (input - elev[x, y]);// -water_depth[x, y]; CHECK THIS IS RIGHT
                        //water_depth[x, y] = input - elev[x, y];
                        water_depth[x, y] = dhdt;

                        // TEMP_V1 - assign/mix temperature of tidal/stage input water.
                        // NOTE: mirrors the existing watertracer logic just below, which also treats
                        // "dhdt" as an added depth even though water_depth[x,y] is fully overwritten above
                        // (a pre-existing quirk in the base code, flagged there as "CHECK THIS IS RIGHT" -
                        // not something this step attempts to fix).
                        if (isSimulateTemperature == true)
                        {
                            double sourceTemp_tidal = get_source_temperature(0, cycle); // TEMP_V1 - stage/tide is always source index 0
                            if (prev_depth <= 0.0 || water_temp[x, y] == -9999)
                            {
                                water_temp[x, y] = sourceTemp_tidal;
                            }
                            else
                            {
                                water_temp[x, y] = ((water_temp[x, y] * prev_depth) + (sourceTemp_tidal * dhdt)) / water_depth[x, y];
                            }
                        }


                        // code below is commented out but where you can add Suspended sediment input at tidal boundary
                        //if (water_depth[x, y] > 0) Vsusptot[x, y] = water_depth[x, y] * 0.001;//0.0005 is 500mg l.. approx.


                        // Adjust water proportions following the addition of stage input - MDW 13/03/16
                        if (isTraceWater == true)
                        {
                            if (isTraceWater == true)
                            {
                                // MDW_V2 - updated source index of stage input to zero

                                if (water_depth[x, y] == dhdt) // if depth was zero, all water in this cell is from this input
                                {
                                    watertracer[x, y, 0] = 1.0; // stage input is assigned to the second layer

                                    // MDW_Apr24: reset other tracers to zero here: edge cells could be zero depth as tide is flows out
                                    for (int src = 1; src < nSources; src++)
                                    {
                                        watertracer[x, y, src] = 0.0;
                                    }
                                }
                                else if (watertracer[x, y, 0] < 1.0) // if all water is already from this source, no need to update
                                {
                                    h_from_this_source = watertracer[x, y, 0] * prev_depth; // amount of water

                                    // update this layer water proportion - i.e. increase
                                    watertracer[x, y, 0] = (h_from_this_source + dhdt) / water_depth[x, y];

                                    // update other layers water proportions - i.e. reduce
                                    for (int src = 1; src < nSources; src++) // MDW_V2 updated to zero index, starts at 1 to skip this layer
                                    {
                                        //if (src != 2) // skip this source layer // check not needed now
                                        //{
                                        watertracer[x, y, src] = (watertracer[x, y, src] * prev_depth) / water_depth[x, y];
                                        //}
                                    }

                                    // MDW_DEBUG
                                    // check for error
                                    /* double tracersum = 0.0;
                                    for (int src = 0; src < nSources; src++)
                                    {
                                        tracersum += watertracer[x, y, src];
                                        if (tracersum > 1.0000001)
                                        {
                                            MessageBox.Show("Tracer exception caught: tracersum = " + Convert.ToString(tracersum) +
                                                ", counter = " + Convert.ToString(counter) + ", cycle = " + Convert.ToString(cycle));
                                        }
                                    } */
                                }

                                // MDW_V2 adjust solute concentrations given stage/ tidal input
                                if ((isTraceSolutes == true) && (water_depth[x, y] > 0.0))
                                {
                                    for (int solute = 0; solute < nSolutes; solute++)
                                    {
                                        if (solutetracer[x, y, solute] > 0 && dhdt > 0) // if nothing is in the cell, and there's no input, skip
                                        {
                                            // update solute concentration in cell: dilution with additional stage input
                                            solutetracer[x, y, solute] = prev_depth * solutetracer[x, y, solute] / water_depth[x, y];
                                        }
                                    }
                                }

                            }
                        }
                    }
                }

            }

        }

        bool oil_spill_ended = false;
        void oil_spill_input(double local_time_factor)

        {

            //double local_time_factor = time_factor;

            // MDW : note to check : we're changing the value of dt used here, so it is different to the one used for Q
            // see stage_tide_input(): local_time_factor is passed into the function rather than calculated within it
            iteration_t += local_time_factor;

            //if (local_time_factor > (courant_number * (DX / Math.Sqrt(gravity * (maxdepth))))) local_time_factor = courant_number * (DX / Math.Sqrt(gravity * (maxdepth)));

            for (int x = Math.Min(oil_fromx, oil_tox); x <= Math.Max(oil_fromx, oil_tox); x++)
            {
                for (int y = Math.Min(oil_fromy, oil_toy); y <= Math.Max(oil_fromy, oil_toy); y++)
                {

                    // condition to define the end of the spill 
                    double spill_end = oil_starth + oil_spillt;

                    
                    if (iteration_t >= oil_starth && iteration_t < spill_end)
                    {
                        oil_ratev = oil_totalv / oil_spillt;

                        // Vol in int the timestep
                        double volume_this_step = oil_ratev * local_time_factor;

                        // Cumulate V oil
                        oil_in_sum += volume_this_step;

                        // thickness added of each cell in the input
                        double depth_increment = volume_this_step / (oil_n_cells * DX * DX);

                        // oil depth update 
                        oil_depth[x, y] += depth_increment;
                    }
                    if ((iteration_t + local_time_factor > spill_end) && !oil_spill_ended)

                    {
                        //oil_in_sum = oil_totalv;

                        // When the spill duration ends, no more oil is added to the cell
                        if (oil_spill_ended = true)
                        {
                            oil_ratev = 0;
                            oil_in_sum = oil_totalv;
                            oil_depth[x, y] += 0;
                        }

                    }


                }
            }
        }








        void soil_development() // all based on Vanwalleghem et al., 2013 (JoGR:ES)
        {
            for (int x = 1; x <= xmax; x++)
            {
                for (int y = 1; y <= ymax; y++)
                {
                    if (elev[x, y] > -9999) // ensure it is not a no-data point
                    {
                        if (index[x, y] == -9999) addGS(x, y); // first ensure that there is grainsize defined for that cell

                        if (checkBox4.Checked) // Bedrock lowering
                        {
                            if (bedrock[x, y] > -9999 && elev[x, y] >= bedrock[x, y])
                            {
                                double h = elev[x, y] - bedrock[x, y];
                                if (h == 0) h = 0.001;
                                bedrock[x, y] += -P1 * Math.Exp(-b1 * (h)) / 12; //  / 12 to make it months
                            }
                        }

                        if (checkBox5.Checked) // Physical Weathering
                        {
                            //amt = k1 * Math.Exp(-c1*depth_below_surface) * (c2/Math.Log10(particle_size) * time;
                            // start from top then work down - means no need to hold values in temp arrays...
                            double Di = 0;
                            int xyindex = index[x, y];


                            for (int n = 2; n <= (G_MAX - 1); n++)
                            {
                                for (int T = 0; T <= tracers; T++)
                                {
                                    if ((grain[xyindex, n, T] > 0.0))
                                    {
                                        switch (n)
                                        {
                                            case 1: Di = d1; break;
                                            case 2: Di = d2; break;
                                            case 3: Di = d3; break;
                                            case 4: Di = d4; break;
                                            case 5: Di = d5; break;
                                            case 6: Di = d6; break;
                                            case 7: Di = d7; break;
                                            case 8: Di = d8; break;
                                            case 9: Di = d9; break;
                                        }

                                        double amount = grain[xyindex, n, T] * ((-(k1 * Math.Exp(-c1 * active * 0.5) * (c2 / Math.Log(Di)) * 1)) / 12); //  / 12 to make it months
                                        grain[xyindex, n, T] -= amount;
                                        if (n == 2)
                                        {
                                            grain[xyindex, n - 1, T] += amount;
                                        }
                                        else
                                        {
                                            grain[xyindex, n - 1, T] += amount * 0.05;
                                            grain[xyindex, n - 2, T] += amount * 0.95;
                                        }

                                        for (int z = 1; z <= 9; z++)
                                        {
                                            double amount2 = strata[xyindex, z - 1, n, T] * ((-(k1 * Math.Exp(-c1 * active * z) * (c2 / Math.Log(Di)) * 1)) / 12); //  / 12 to make it months

                                            strata[xyindex, z - 1, n, T] -= amount;
                                            if (n == 2)
                                            {
                                                strata[xyindex, z - 1, n - 1, T] += amount;
                                            }
                                            else
                                            {
                                                strata[xyindex, z - 1, n - 1, T] += amount * 0.05;
                                                strata[xyindex, z - 1, n - 2, T] += amount * 0.95;
                                            }


                                        }
                                    }
                                }
                            }

                            //for (int z = 9; z >= 1; z--)
                            //{
                            //    for (int n = 0; n <= G_MAX - 2; n++)
                            //    {

                            //        strata[xyindex, z, n] = strata[xyindex, z - 1, n];

                            //    }
                            //}


                        }

                        if (checkBox6.Checked) // Chemical Weathering
                        {
                            //amt = k2 * Math.Exp(-c3 * depth_below_surface) * c4 * Specific_surface_area * time;
                        }
                    }
                }
            }
        }

        void get_catchment_input_points()
        {
            totalinputpoints = 0;
            for (int n = 1; n <= rfnum; n++) catchment_input_counter[n] = 0;
            for (int x = 1; x <= xmax; x++)
            {
                for (int y = 1; y <= ymax; y++)
                {
                    if ((area[x, y] * baseflow * DX * DX) > MIN_Q && (area[x, y] * baseflow * DX * DX) < Convert.ToDouble(MinQmaxvalue.Text))
                    {
                        totalinputpoints++;
                        catchment_input_x_coord[totalinputpoints] = x;
                        catchment_input_y_coord[totalinputpoints] = y;
                        catchment_input_counter[rfarea[x, y]]++;
                    }
                }
            }
            // could be the below line causing problems..?
            //if (totalinputpoints == 0) totalinputpoints = 1;
        }

        void evaporate(double time)
        {
            double evap_amount = k_evap * (time / 1440);
            // now reduce if greater than erodedepo - to prevent instability
            if (evap_amount > ERODEFACTOR) evap_amount = ERODEFACTOR;

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;
                    // removes water in rate of mm per day..
                    if (water_depth[x, y] > 0)
                    {
                        double prev_depth = water_depth[x, y]; // MDW_V2 needed below

                        water_depth[x, y] -= evap_amount;
                        if (water_depth[x, y] < 0) water_depth[x, y] = 0;

                        // MDW_Apr24: disabled pending figuring out a better method - causes very high concentrations as depth goes to zero
                        // MDW_V2 : concentrate solutes as evaporation occurs - note: currently this does not handle what happens if a cell dries out
                        //if ((isTraceSolutes == true) && (water_depth[x, y] > 0.0))
                        //{
                        //    for (int solute = 0; solute < nSolutes; solute++)
                        //    {
                        //        if (solutetracer[x, y, solute] > 0 && evap_amount > 0) // if nothing is in the cell, and there's no evaporation, skip
                        //        {
                        //            if(evap_amount < prev_depth) // MDW_Apr24, avoid div zero exception if cell dries
                        //            {
                        //                // update solute concentration in cell: concentation with evaporation
                        //                solutetracer[x, y, solute] = prev_depth * solutetracer[x, y, solute] / water_depth[x, y]; // proportion of depth in cell which is old * solute existing
                        //           } else
                        //            {
                        //                solutetracer[x, y, solute] = 0.0; // no water so no concentration. Not ideal as would remain on surface.
                        //                // would perhaps be better to keep the previous concentration in a "surface deposition" layer
                        //            }
                        //        }
                        //    }
                        //}
                    }

                }

            });
        }

        void save_data_every1000iters() // MDW_V2: unreferenced
        {
            if (menuItem25.Checked == true) save_data(1, 0); // save waterdepths
            if (menuItem13.Checked == true) save_data(2, 0); // save elevdiff
            if (menuItem12.Checked == true) save_data(3, 0); // save elevations
            if (menuItem14.Checked == true) save_data(4, 0); // save grainsize
            if (menuItem29.Checked == true) save_data(15, 0); // save d50 top layer
            if (menuItem33.Checked == true) save_data(16, 0); // save velocity	<JOE 20050605>
            if (menuItem34.Checked == true) save_data(17, 0); // save soil_saturation	<JOE 20050605>
            if (menuItem6.Checked == true) save_data(18, 0); // save water tracers - MDW 17-03-2016
            if (menuItem15.Checked == true) save_data(19, 0); // save rain zone tracers - MDW 13-04-2016
            if (menuItem16.Checked == true) save_data(6, 0); // save tracer file
            if (menuItemSoluteTracer.Checked == true) save_data(20, 0); // save solute tracers - MDW_V2
            if (menuItemOilSpill.Checked == true) save_data(7, 0); // save oildepth file //OIL_V1_PDF
            if (menuItem17.Checked == true) save_data(8, 0); // save mass balance file //OIL_V1_PDF
            if (menuItemOilconc.Checked == true) save_data(9, 0); // save oil concentration file //OIL_V1_PDF)
            slide_5();

        }

        void save_data_and_draw_graphics()
        {
            if (menuItem25.Checked == true) save_data(1, Math.Abs(cycle)); // save waterdepths
            if (menuItem13.Checked == true) save_data(2, Math.Abs(cycle)); // save elevdiff
            if (menuItem12.Checked == true) save_data(3, Math.Abs(cycle)); // save elevations
            if (menuItem14.Checked == true) save_data(4, Math.Abs(cycle)); // save grainsize
            if (menuItem29.Checked == true) save_data(15, Math.Abs(cycle)); // save d50 top layer
            if (menuItem33.Checked == true) save_data(16, Math.Abs(cycle)); // save velocity			<JOE 20050605>
            if (menuItem34.Checked == true) save_data(17, Math.Abs(cycle)); // save soil saturation	<JOE 20050605
            if (menuItem6.Checked == true) save_data(18, Math.Abs(cycle)); // save water tracers - MDW 17-03-2016
            if (menuItem15.Checked == true) save_data(19, Math.Abs(cycle)); // save rain zone tracers - MDW 13-04-2016
            if (menuItem16.Checked == true) save_data(6, Math.Abs(cycle)); // save tracer file
            if (menuItemSoluteTracer.Checked == true) save_data(20, Math.Abs(cycle)); // save solute tracers - MDW_V2
            if (menuItemOilSpill.Checked == true) save_data(7, Math.Abs(cycle)); // save oil depth - PDF
            if (menuItem17.Checked == true) save_data(8, Math.Abs(cycle)); // save mass balance - PDF
            if (menuItemOilconc.Checked == true) save_data(9, Math.Abs(cycle)); // save oil concentration - PDF



        }

        void update_screen_outputs(object sender, System.EventArgs e)
        {
            this.IterationStatusPanel.Text = "it = " + counter.ToString();  // MJ 14/01/05
            if (cycle <= 1440)
            {
                this.TimeStatusPanel.Text = string.Format("t = {0:F6} min", cycle);
            }
            else
            {
                this.TimeStatusPanel.Text = string.Format("t = {0:F6} day", cycle / 1440);
            }


            // now display sedi out bottom..
            if (menuItem4.Checked == false) this.QsStatusPanel.Text = string.Format("Qs = {0:F8}", sediQ); // MJ 14/01/05
            // updates the text boxes with the time and iterationbs elapsed
            this.QwStatusPanel.Text = string.Format("Qw = {0:G}", waterOut); // MJ 14/01/05

            if (googleoutputflag)
            {
                googleoutputflag = false;
                updateClick = 1;
                this.Refresh();
                drawwater(mygraphics);
                if (coordinateDone == 0)
                {
                    //transfrom coordinates
                    point testPoint = new point(xll, yll);
                    if (UTMgridcheckbox.Checked)
                    {
                        testPoint.UTMzone = System.Convert.ToInt32(UTMzonebox.Text);
                        testPoint.south = System.Convert.ToBoolean(UTMsouthcheck.Checked);
                        testPoint.transformUTMPoint();
                    }
                    else
                    {
                        testPoint.transformPoint();
                    }
                    yurcorner = yll + (System.Convert.ToDouble(ymax) * System.Convert.ToDouble(DX));
                    xurcorner = xll + (System.Convert.ToDouble(xmax) * System.Convert.ToDouble(DX));
                    point testPoint2 = new point(xurcorner, yurcorner);
                    if (UTMgridcheckbox.Checked)
                    {
                        testPoint2.UTMzone = System.Convert.ToInt32(UTMzonebox.Text);
                        testPoint2.south = System.Convert.ToBoolean(UTMsouthcheck.Checked);
                        testPoint2.transformUTMPoint();
                    }
                    else
                    {
                        testPoint2.transformPoint();
                    }
                    urfinalLati = testPoint2.ycoord;
                    urfinalLongi = testPoint2.xcoord;
                    llfinalLati = testPoint.ycoord;
                    llfinalLongi = testPoint.xcoord;
                    coordinateDone = 1;
                }
                //Save image
                m_objDrawingSurface.MakeTransparent();
                m_objDrawingSurface.Save(Path.Combine(googleAnimationDir, "mysavedimage" + imageCount2 + ".png"), // MDW_V2 updated to point to correct folder
                                         System.Drawing.Imaging.ImageFormat.Png);
                //m_objDrawingSurface.Save("animation\\mysavedimage" + imageCount2 + ".png", System.Drawing.Imaging.ImageFormat.Png);
                //update time
                googleTime = googleTime.AddMinutes(save_interval2);
                kmlTime = googleTime.ToString();
                DateArray = kmlTime.Split(new char[] { ' ' });
                DateArray2 = DateArray[0].Split(new char[] { '/' });
                kmlTime = DateArray2[2] + "-" + DateArray2[1] + "-" + DateArray2[0] + "T" + DateArray[1] + "Z";

                //create kml file for image
                StreamWriter kmlsr = File.AppendText(KML_FILE_NAME);
                if (imageCount2 == 1)
                {
                    kml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                         <kml xmlns=""http://earth.google.com/kml/2.1"">";
                    kml = kml + "\n<Folder>"
                        + "\n<name>Animation</name>";
                    kmlsr.WriteLine(kml);
                    kml = "";
                }
                kml = kml + "\n<GroundOverlay>"
                    + "\n<name>Untitled Image Overlay</name>";
                kml = kml + "\n<TimeSpan>"
                       + "\n<begin>" + kmlTime + "</begin>"
                       + "\n<end>" + kmlTime + "</end>"
                       + "\n</TimeSpan>"
                       + "\n<Icon>"
                       + "\n<href>mySavedImage" + imageCount2 + ".png</href>"
                       + "\n</Icon>"
                       + "\n<LatLonBox>";
                kml = kml + "\n<north>" + urfinalLati + "</north>"
                      + "\n<south>" + llfinalLati + "</south>"
                      + "\n<east>" + urfinalLongi + "</east>"
                      + "\n<west>" + llfinalLongi + "</west>\n";
                kml = kml + @"</LatLonBox>
                           </GroundOverlay>";
                kmlsr.WriteLine(kml);
                kml = "";
                kmlsr.Close();
                //imageCount2 = imageCount2 + 1;
                //save_time2 += save_interval2;



                //create pngw file for image - for quick loading of graphics in GIS (MDW 18-04-16)
                StreamWriter pngw = File.AppendText(Path.Combine(googleAnimationDir, "mysavedimage" + imageCount2 + ".pngw")); // MDW_V2 updated to new folder
                //StreamWriter pngw = File.AppendText("animation\\mysavedimage" + imageCount2 + ".pngw");
                System.Drawing.SizeF xy = m_objDrawingSurface.PhysicalDimension;
                string pngwtext = "";
                pngwtext = pngwtext + Convert.ToString(Math.Abs(llfinalLongi - urfinalLongi) / xy.Width) + "\n";
                pngwtext = pngwtext + "0\n0\n";
                pngwtext = pngwtext + Convert.ToString((Math.Abs(llfinalLati - urfinalLati) / xy.Height) * -1) + "\n";
                pngwtext = pngwtext + llfinalLongi + "\n";
                pngwtext = pngwtext + urfinalLati + "\n";
                pngw.WriteLine(pngwtext);
                pngw.Close();

            }
        }

        void calchydrograph(double time)
        {
            for (int n = 1; n <= rfnum; n++)
            {
                j_mean[n] = old_j_mean[n] + (((new_j_mean[n] - old_j_mean[n]) / 2) * (2 - time));
            }
        }



        void output_data()
        {
            int n;

            // Qw (m3) value at timestep cycle (current
            // Qw_newvol+=temptotal*((cycle-previous)*output_file_save_interval); // replaced by line below MJ 25/01/05
            Qw_newvol += temptotal * ((cycle - previous) * 60);  // 60 secs per min
            for (int nn = 1; nn <= rfnum; nn++) Jw_newvol += (j_mean[nn] * DX * DX * nActualGridCells[nn]) * ((cycle - previous) * 60);

            //Catch all time steps that pass one or more hour marks
            if ((new_cycle < old_cycle) || (cycle - previous >= output_file_save_interval))
            {
                while ((tx > previous) && (cycle >= tx))
                {
                    hours++;

                    // Step1: Calculate hourly total sediment Q (m3)
                    Qs_step = globalsediq - old_sediq;
                    Qs_over = Qs_step * ((cycle - tx) / (cycle - tlastcalc));
                    Qs_hour = Qs_step - Qs_over + Qs_last;

                    // reset Qs_last and old_sediq for large time steps
                    if (cycle >= tx + output_file_save_interval)
                    {
                        Qs_last = 0;
                        old_sediq = globalsediq - Qs_over;
                    }

                    // reset Qs_last and old_sediq for small time steps
                    if (cycle < tx + output_file_save_interval)
                    {
                        Qs_last = Qs_over;
                        old_sediq = globalsediq;
                    }

                    // Step 2: Calculate grain size Qgs, also calculate contaminated amounts
                    for (int T = 0; T <= tracers; T++)
                    {
                        for (n = 1; n <= G_MAX - 1; n++)
                        {
                            // calculate timestep Qgs
                            Qg_step[n, T] = sum_grain[n, T] - old_sum_grain[n, T];
                            Qg_step2[n, T] = sum_grain2[n, T] - old_sum_grain2[n, T];
                            // Interpolate Qgs beyond time tx
                            Qg_over[n, T] = Qg_step[n, T] * ((cycle - tx) / (cycle - tlastcalc));
                            Qg_over2[n, T] = Qg_step2[n, T] * ((cycle - tx) / (cycle - tlastcalc));
                            // and calculate hourly Qgs
                            Qg_hour[n, T] = Qg_step[n, T] - Qg_over[n, T] + Qg_last[n, T];
                            Qg_hour2[n, T] = Qg_step2[n, T] - Qg_over2[n, T] + Qg_last2[n, T];
                            // Reset Qg_last[n , T] and old_sum_grain[n , T]for large time steps
                            if (cycle >= tx + output_file_save_interval)
                            {
                                Qg_last[n, T] = 0;
                                Qg_last2[n, T] = 0;
                                old_sum_grain[n, T] = sum_grain[n, T] - Qg_over[n, T];
                                old_sum_grain2[n, T] = sum_grain2[n, T] - Qg_over2[n, T];
                            }
                            // Reset Qg_last[n , T] and old_sum_grain[n , T] for small time steps
                            if (cycle < tx + output_file_save_interval)
                            {
                                Qg_last[n, T] = Qg_over[n, T];
                                Qg_last2[n, T] = Qg_over2[n, T];
                                old_sum_grain[n, T] = sum_grain[n, T];
                                old_sum_grain2[n, T] = sum_grain2[n, T];
                            }
                        }
                    }

                    // Step 3: Calculate hourly mean water discharge
                    // Qw_overvol = temptotal*((cycle-tx)*output_file_save_interval); // replaced by line below MJ 25/01/05
                    Qw_overvol = temptotal * ((cycle - tx) * 60);   // 60 secs per min
                    Qw_stepvol = Qw_newvol - Qw_oldvol;
                    Qw_hourvol = Qw_stepvol - Qw_overvol + Qw_lastvol;
                    Qw_hour = Qw_hourvol / (60 * output_file_save_interval); // convert hourly water volume to cumecs

                    // same for Jw (j_mean contribution)  MJ 14/03/05
                    for (int nn = 1; nn <= rfnum; nn++) Jw_overvol = (j_mean[nn] * DX * DX * nActualGridCells[nn]) * ((cycle - tx) * 60);  // fixed MJ 29/03/05
                    Jw_stepvol = Jw_newvol - Jw_oldvol;
                    Jw_hourvol = Jw_stepvol - Jw_overvol + Jw_lastvol;
                    Jw_hour = Jw_hourvol / (60 * output_file_save_interval);


                    // reset Qw_lastvol and Qw_oldvol for large time steps
                    if (cycle >= tx + output_file_save_interval)
                    {
                        Qw_lastvol = 0;
                        Qw_oldvol = Qw_newvol - Qw_overvol;

                        // same for Jw (j_mean contribution)  MJ 14/03/05
                        Jw_lastvol = 0;
                        Jw_oldvol = Jw_newvol - Jw_overvol;
                    }

                    // reset Qw_lastvol and Qw_oldvol for small time steps
                    if (cycle < tx + output_file_save_interval)
                    {
                        Qw_lastvol = Qw_overvol;
                        Qw_oldvol = Qw_newvol;

                        // same for Jw (j_mean contribution)  MJ 14/03/05
                        Jw_lastvol = Jw_overvol;
                        Jw_oldvol = Jw_newvol;
                    }

                    Tx = tx;
                    tx = Tx + output_file_save_interval;

                    for (int T = 0; T <= tracers; T++)
                    {
                        // Step 4: Output hourly data to file (format for reach model input)
                        // changed MJ 18/01/05
                        string output = string.Format("{0}", hours);
                        output = output + string.Format(" {0:F6}", Qw_hour);
                        output = output + string.Format(" {0:F6}", Jw_hour);
                        if (SiberiaBox.Checked == true)
                        {
                            double tomsedi = 0;
                            for (int x = 1; x <= xmax; x++)
                            {
                                for (int y = 1; y <= ymax; y++)
                                {
                                    if (elev[x, y] > -9999)
                                    {
                                        tomsedi += (init_elevs[x, y] - elev[x, y]) * DX * DX;
                                    }
                                }
                            }
                            output = output + string.Format(" {0:F6}", tomsedi);
                        }
                        else
                        {
                            output = output + string.Format(" {0:F6}", sand_out);
                            sand_out = 0;
                        }
                        output = output + string.Format(" {0:F10}", Qs_hour);
                        for (n = 1; n <= G_MAX - 1; n++)
                        {
                            output = output + string.Format(" {0:F10}", Qg_hour[n, T]);
                            //output = output+" "+Qg_hour[n];
                        }

                        try
                        {
                            StreamWriter sw = File.AppendText(Convert.ToString(T) + CATCH_FILE);
                            sw.WriteLine(output);
                            sw.Close();
                        }
                        catch (Exception)
                        {

                        }

                    }

                }
                tlastcalc = cycle;
            }
        }


        int read_header()
        {
            string FILE_NAME;
            int z;
            string[] lineArray2;
            int sp;

            inputheader = new string[6];

            FILE_NAME = this.openfiletextbox.Text;
            if (!File.Exists(FILE_NAME))
            {
                MessageBox.Show("No such DEM data file..");
                return 0;
            }

            try
            {

                //read headers
                StreamReader sr = File.OpenText(FILE_NAME);
                for (z = 1; z <= 6; z++)
                {
                    inputheader[z - 1] = sr.ReadLine();
                }
                sr.Close();

                // get xmax, ymax and DX from input headers

                lineArray2 = inputheader[0].Split(new char[] { ' ' });
                sp = 1;
                while (lineArray2[sp] == "") sp++;
                xmax = int.Parse(lineArray2[sp]);

                lineArray2 = inputheader[1].Split(new char[] { ' ' });
                sp = 1;
                while (lineArray2[sp] == "") sp++;
                ymax = int.Parse(lineArray2[sp]);

                lineArray2 = inputheader[2].Split(new char[] { ' ' });
                sp = 1;
                while (lineArray2[sp] == "") sp++;
                xll = double.Parse(lineArray2[sp]);

                lineArray2 = inputheader[3].Split(new char[] { ' ' });
                sp = 1;
                while (lineArray2[sp] == "") sp++;
                yll = double.Parse(lineArray2[sp]);

                lineArray2 = inputheader[4].Split(new char[] { ' ' });
                sp = 1;
                while (lineArray2[sp] == "") sp++;
                DX = double.Parse(lineArray2[sp]);
                root = (Math.Sqrt(Math.Pow(DX, 2) + Math.Pow(DX, 2))); // imp added 11/07

            }
            catch (Exception e)
            {
                MessageBox.Show("Theres a problem with the header in the DEM file" +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message");
            }

            return 1;
        }


        void load_data()
        {
            int x, y = 1, z, xcounter, x1 = 0, y1 = 0, n;
            String input;
            double tttt = 0.00;
            int eee = 0;
            int iiii = 0;

            char[] delimiterChars = { ' ', ',', '\t' };

            // load dem

            string FILE_NAME = this.openfiletextbox.Text;
            if (!File.Exists(FILE_NAME))
            {
                MessageBox.Show("No such DEM data file..");
                return;
            }

            StreamReader sr = File.OpenText(FILE_NAME);

            //read headers
            for (z = 1; z <= 6; z++)
            {
                input = sr.ReadLine();
            }
            y = 1;

            while ((input = sr.ReadLine()) != null)
            {
                string[] lineArray;
                lineArray = input.Split(delimiterChars);
                xcounter = 1;
                for (x = 0; x <= (lineArray.Length - 1); x++)
                {

                    if (lineArray[x] != "" && xcounter <= xmax)
                    {
                        tttt = double.Parse(lineArray[x]);
                        elev[xcounter, y] = tttt;
                        //if(xcounter==1)elev[xcounter,y]+=4;
                        init_elevs[xcounter, y] = elev[xcounter, y];
                        xcounter++;
                    }
                }
                y++;

            }
            sr.Close();

            // load hydro coverage
            ///////////////////////////////////////

            try
            {
                FILE_NAME = this.hydroindexBox.Text;
                if (FILE_NAME != "null")
                {
                    sr = File.OpenText(FILE_NAME);

                    //read headers
                    for (z = 1; z <= 6; z++)
                    {
                        input = sr.ReadLine();
                    }
                    y = 1;

                    while ((input = sr.ReadLine()) != null)
                    {
                        string[] lineArray;
                        lineArray = input.Split(delimiterChars);
                        xcounter = 1;
                        for (x = 0; x <= (lineArray.Length - 1); x++)
                        {

                            if (lineArray[x] != "" && xcounter <= xmax)
                            {
                                int xxxx = int.Parse(lineArray[x]);
                                if (xxxx == -9999) xxxx = 1;
                                rfarea[xcounter, y] = xxxx;
                                xcounter++;
                            }
                        }
                        y++;

                    }
                    sr.Close();
                }
            }
            catch (Exception)
            {

            }

            // load rainfall zonation map for trace water - MDW 01/04/16
            ///////////////////////////////////////
            if (this.checkBox11.Checked == true)
            {
                try
                {
                    FILE_NAME = this.textBox20.Text;
                    bool zoneZeroDefined = false; // MDW_V2

                    if (FILE_NAME != "null")
                    {
                        sr = File.OpenText(FILE_NAME);

                        //read headers
                        for (z = 1; z <= 6; z++)
                        {
                            input = sr.ReadLine();
                        }
                        y = 1;

                        while ((input = sr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            xcounter = 1;
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {

                                if (lineArray[x] != "" && xcounter <= xmax)
                                {
                                    iiii = int.Parse(lineArray[x]);
                                    if (iiii == 0) zoneZeroDefined = true; // MDW_V2
                                    rainzonation[xcounter, y] = iiii;
                                    xcounter++;
                                }
                            }
                            y++;

                        }
                        sr.Close();

                        // MDW_V2 updated to fix overflow bug
                        // get a unique list of rainzones, ignoring -9999 values and zeros if they are not defined as zones
                        int[] zlist = rainzonation.Cast<int>().Distinct().ToArray();

                        for (int i = 0; i < zlist.Length; i++)
                        {
                            if (zlist[i] == -9999) { continue; }
                            else if (zoneZeroDefined == false && zlist[i] == 0) { continue; }
                            else
                            {
                                Array.Resize(ref rainZones, rainZones.Length + 1); // contract to number of zones
                                rainZones[nRainZones] = zlist[i];
                                nRainZones++;
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Rainfall zonation map not found. Deactivating.");
                        this.checkBox11.Checked = false;
                        isTraceRainZonation = false;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("Rainfall zonation error. Deactivating." +
                        "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message

                    this.checkBox11.Checked = false;
                    isTraceRainZonation = false;
                    return;
                }
            }

            if (this.graindataloadbox.Text != "null")
            {
                FILE_NAME = this.graindataloadbox.Text;
                if (FILE_NAME != "null")
                {
                    StreamReader gr = File.OpenText(FILE_NAME);
                    y = 1;
                    grain_array_tot = 0;
                    while ((input = gr.ReadLine()) != null)
                    {
                        string[] lineArray;
                        lineArray = input.Split(delimiterChars);
                        xcounter = 1;
                        grain_array_tot++;
                        for (x = 0; x <= (lineArray.Length - 1); x++)
                        {
                            if (lineArray[x] != "")
                            {
                                if (xcounter == 1) x1 = int.Parse(lineArray[x]);
                                if (xcounter == 2) y1 = int.Parse(lineArray[x]);
                                if (x1 > xmax) x1 = xmax; // lines to prevent adding grain if clipping the grid and excellss on the grain grid is left
                                if (y1 > ymax) y1 = ymax; //

                                if (xcounter == 3) index[x1, y1] = grain_array_tot;// int.Parse(lineArray[x]);

                                for (n = 0; n <= G_MAX; n++)
                                {
                                    if (xcounter == 4 + n)
                                    {
                                        grain[grain_array_tot, n, 0] = double.Parse(lineArray[x]);
                                    }
                                }

                                for (z = 0; z <= 9; z++)
                                {
                                    for (n = 0; n <= (G_MAX - 2); n++)
                                    {
                                        if (xcounter == (4 + G_MAX + n + 1) + ((z) * 9))
                                        {
                                            strata[grain_array_tot, z, n, 0] = double.Parse(lineArray[x]);
                                        }
                                    }
                                }

                                xcounter++;
                            }
                        }
                    }
                    gr.Close();
                }
            }


            FILE_NAME = this.bedrockbox.Text;
            if (FILE_NAME != "null")
            {

                StreamReader gr = File.OpenText(FILE_NAME);
                //read headers
                for (z = 1; z <= 6; z++)
                {
                    input = gr.ReadLine();
                }
                y = 1;

                while ((input = gr.ReadLine()) != null)
                {
                    string[] lineArray;
                    lineArray = input.Split(delimiterChars);
                    xcounter = 1;
                    for (x = 0; x <= (lineArray.Length - 1); x++)
                    {

                        if (lineArray[x] != "" && xcounter <= xmax)
                        {
                            tttt = double.Parse(lineArray[x]);
                            bedrock[xcounter, y] = tttt;
                            xcounter++;
                        }
                    }
                    y++;

                }
                gr.Close();
            }

            FILE_NAME = this.angle_thresholdbox.Text;
            if (FILE_NAME != "null")
            {

                StreamReader gr = File.OpenText(FILE_NAME);
                //read headers
                for (z = 1; z <= 6; z++)
                {
                    input = gr.ReadLine();
                }
                y = 1;

                while ((input = gr.ReadLine()) != null)
                {
                    string[] lineArray;
                    lineArray = input.Split(delimiterChars);
                    xcounter = 1;
                    for (x = 0; x <= (lineArray.Length - 1); x++)
                    {

                        if (lineArray[x] != "" && xcounter <= xmax)
                        {
                            eee = int.Parse(lineArray[x]);
                            angle_threshold[xcounter, y] = eee;
                            xcounter++;
                        }
                    }
                    y++;

                }
                gr.Close();
            }


            FILE_NAME = this.tracer_file.Text;
            if (FILE_NAME != "null")
            {

                StreamReader gr = File.OpenText(FILE_NAME);
                //read headers
                for (z = 1; z <= 6; z++)
                {
                    input = gr.ReadLine();
                }
                y = 1;

                while ((input = gr.ReadLine()) != null)
                {
                    string[] lineArray;
                    lineArray = input.Split(delimiterChars);
                    xcounter = 1;
                    for (x = 0; x <= (lineArray.Length - 1); x++)
                    {

                        if (lineArray[x] != "" && xcounter <= xmax)
                        {
                            eee = int.Parse(lineArray[x]);
                            tracer_area[xcounter, y] = eee;
                            xcounter++;
                        }
                    }
                    y++;

                }
                gr.Close();
            }

            FILE_NAME = this.grain_index_file.Text;
            if (FILE_NAME != "null")
            {

                StreamReader gr = File.OpenText(FILE_NAME);
                //read headers
                for (z = 1; z <= 6; z++)
                {
                    input = gr.ReadLine();
                }
                y = 1;

                while ((input = gr.ReadLine()) != null)
                {
                    string[] lineArray;
                    lineArray = input.Split(delimiterChars);
                    xcounter = 1;
                    for (x = 0; x <= (lineArray.Length - 1); x++)
                    {

                        if (lineArray[x] != "" && xcounter <= xmax)
                        {
                            eee = int.Parse(lineArray[x]);
                            grain_area[xcounter, y] = eee;
                            xcounter++;
                        }
                    }
                    y++;

                }
                gr.Close();
            }

            int inc1 = 1;

            try
            {


                FILE_NAME = this.raindataloadbox.Text;
                if (FILE_NAME != "null")
                {

                    int max_file_length = (int)(maxcycle * (60 / rain_data_time_step)) + 100;

                    //int inc = 1;
                    StreamReader gr = File.OpenText(FILE_NAME);
                    while ((input = gr.ReadLine()) != null && inc1 < max_file_length - 1) // MDW_Apr24: added to prevent overrun if longer file used
                    {
                        string[] lineArray;
                        lineArray = input.Split(delimiterChars);
                        xcounter = 1;
                        //MessageBox.Show(Convert.ToString(inc));
                        for (x = 0; x <= (lineArray.Length - 1); x++)
                        {
                            if (xcounter > rfnum) continue; // MDW_V2 prevent overflow for additional rainfall columns if rfnum isn't specified

                            if (lineArray[x] != "")
                            {
                                tttt = double.Parse(lineArray[x]);
                                hourly_rain_data[inc1, xcounter] = tttt;

                                xcounter++;
                            }
                        }
                        inc1++;
                    }

                    gr.Close();
                }
            }

            catch (Exception e)
            {
                MessageBox.Show("There was some type of error loading the input data from the rain data file at line " + Convert.ToString(inc1) + ", CAESAR will continue to function but may not be correct." +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message);
                //MessageBox.Show(" There was some type of error loading the input data from the rain data file CAESAR may continue to function but may not be correct");
            }

            try
            {
                //FILE_NAME = this.mvalueloadbox.Text;
                //if (FILE_NAME != "null")
                //{
                //    variable_m_value_flag = 1; // sets flag for variable M value to 1 (true)
                //    int inc = 1;
                //    StreamReader gr = File.OpenText(FILE_NAME);
                //    while ((input = gr.ReadLine()) != null)
                //    {
                //        hourly_m_value[inc] = double.Parse(input);
                //        inc++;
                //    }
                //    gr.Close();
                //}
                FILE_NAME = this.mvalueloadbox.Text;
                if (FILE_NAME != "null")
                {
                    variable_m_value_flag = 1; // sets flag for variable M value to 1 (true)
                    int inc = 1;
                    int max_file_length = (int)(maxcycle * (60 / mfiletimestep)) + 10;

                    StreamReader gr = File.OpenText(FILE_NAME);
                    while ((input = gr.ReadLine()) != null && inc < max_file_length - 1) // MDW_Apr24: added to prevent overrun if longer input file used
                    {
                        string[] lineArray;
                        lineArray = input.Split(delimiterChars);
                        xcounter = 1;
                        //MessageBox.Show(Convert.ToString(inc));
                        for (x = 0; x <= (lineArray.Length - 1); x++)
                        {
                            if (lineArray[x] != "")
                            {
                                tttt = double.Parse(lineArray[x]);
                                hourly_m_value[inc, xcounter] = tttt;
                                xcounter++;
                            }
                        }
                        inc++;
                    }

                    gr.Close();
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("There was some type of error loading the m value data from the m value data file CAESAR may continue to function but may not be correct." +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message");
            }

            if (checkBox3.Checked == true)
            {
                try
                {
                    FILE_NAME = this.TidalFileName.Text;
                    if (FILE_NAME != "null")
                    {
                        int inc = 1;
                        int max_file_length = (int)((maxcycle * 60) / stage_input_time_step) + 10;

                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null && inc < max_file_length - 1) // MDW_Apr24: Added to prevent overflow if a longer file is used
                        {
                            stage_inputfile[inc] = double.Parse(input);
                            inc++;
                        }
                        gr.Close();
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show("There was some type of error loading the stage/tidal data file CAESAR may continue to function but may not be correct." +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message");
                }
            }

            // load spatially variable mannings
            ///////////////////////////////////////

            if (SpatVarManningsCheckbox.Checked == true)
            {
                try
                {
                    FILE_NAME = this.textBox19.Text;
                    if (FILE_NAME != "null")
                    {
                        sr = File.OpenText(FILE_NAME);

                        //read headers
                        for (z = 1; z <= 6; z++)
                        {
                            input = sr.ReadLine();
                        }
                        y = 1;

                        while ((input = sr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            xcounter = 1;
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {

                                if (lineArray[x] != "" && xcounter <= xmax)
                                {
                                    double xxxx = double.Parse(lineArray[x]);
                                    if (xxxx == -9999) xxxx = 0.04; //failsafe to stop nodatas giving strange results.
                                    spat_var_mannings[xcounter, y] = xxxx;
                                    xcounter++;
                                }
                            }
                            y++;

                        }
                        sr.Close();
                    }
                }
                catch (Exception)
                {

                }
            }

            try
            {

                FILE_NAME = this.mine_input_textBox.Text;
                if (FILE_NAME != "null")
                {
                    StreamReader gr = File.OpenText(FILE_NAME);
                    while ((input = gr.ReadLine()) != null)
                    {
                        minesitenumber++;
                        string[] lineArray;
                        lineArray = input.Split(delimiterChars);
                        for (x = 0; x <= (lineArray.Length - 1); x++)
                        {
                            if (lineArray[x] != "")
                            {
                                mine_inputs[minesitenumber, x] = double.Parse(lineArray[x]);
                            }
                        }
                    }
                    gr.Close();
                }

            }
            catch (Exception e)
            {
                MessageBox.Show("There was some type of error loading the contaminant input CAESAR may continue to function but may not be correct." +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message");
            }
            //int tempx = 0; // MDW_V2 removed after refactoring


            ///////// Add logic code above so that the index for tide/ rain is not included if they are not on
            ///////// The "null" layer in the visualisation will need to be made to work as well though - currently hangs

            //List<string> inputfilenames = new List<string>(); //MDW - moved to global

            // MDW_V2
            // Refactored logic for loading in hydro files to enable easier additions. The key difference is that a new
            // function takes care of the file loading, load_hydrofile()
            // Functionallity has been added to enable handling of missing values within source files, and additional solutes
            //
            try
            {
                if (inbox1.Checked)
                {
                    FILE_NAME = this.infile1.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }

                }
                if (inbox2.Checked)
                {
                    FILE_NAME = this.infile2.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inbox3.Checked)
                {
                    FILE_NAME = this.infile3.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inbox4.Checked)
                {
                    FILE_NAME = this.infile4.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inbox5.Checked)
                {
                    FILE_NAME = this.infile5.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inbox6.Checked)
                {
                    FILE_NAME = this.infile6.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inbox7.Checked)
                {
                    FILE_NAME = this.infile7.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inbox8.Checked)
                {
                    FILE_NAME = this.infile8.Text;
                    if (FILE_NAME != "null")
                    {
                        load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked);
                        nHydroChecked++;
                    }
                }

                if (inboxExtra.Checked)
                {
                    //extraSourceFiles
                    for (int i = 0; i < extraSourceFiles.Length; i++)
                    {
                        FILE_NAME = extraSourceFiles[i];
                        if (FILE_NAME != "null")
                        {
                            load_hydrofile(FILE_NAME, delimiterChars, nHydroChecked + i);
                            //MessageBox.Show("Loaded extra source file: " + FILE_NAME + " (" + Convert.ToString(i+1) + " of " + extraSourceFiles.Length + ")");
                        }
                    }
                }
                // TEMP_V1 - meteorological forcing input files
                if (isSimulateTemperature == true)
                {
                    FILE_NAME = this.TempTab_textBox_airtemp.Text;
                    load_met_file(FILE_NAME, delimiterChars, hourly_air_temp);

                    FILE_NAME = this.TempTab_textBox_shortwave.Text;
                    load_met_file(FILE_NAME, delimiterChars, hourly_shortwave);

                    FILE_NAME = this.TempTab_textBox_windspeed.Text;
                    load_met_file(FILE_NAME, delimiterChars, hourly_windspeed);

                    if (this.TempTab_textBox_humidity.Text != "null")
                        load_met_file(this.TempTab_textBox_humidity.Text, delimiterChars, hourly_humidity);

                    if (this.TempTab_textBox_cloudcover.Text != "null")
                        load_met_file(this.TempTab_textBox_cloudcover.Text, delimiterChars, hourly_cloudcover);

                    if (this.TempTab_textBox_pressure.Text != "null")
                        load_met_file(this.TempTab_textBox_pressure.Text, delimiterChars, hourly_pressure);

                    if (useSimplifiedTempScheme == true && this.TempTab_textBox_dewpoint.Text != "null")
                        load_met_file(this.TempTab_textBox_dewpoint.Text, delimiterChars, hourly_dewpoint);

                    // TEMP_V1 - initial water temperature raster (optional; overrides the constant value set in zero_values())
                    if (this.TempTab_textBox_initialraster.Text != "null")
                    {
                        try
                        {
                            FILE_NAME = this.TempTab_textBox_initialraster.Text;
                            if (File.Exists(FILE_NAME))
                            {
                                sr = File.OpenText(FILE_NAME);
                                for (z = 1; z <= 6; z++)
                                {
                                    input = sr.ReadLine();
                                }
                                y = 1;
                                while ((input = sr.ReadLine()) != null)
                                {
                                    string[] lineArray;
                                    lineArray = input.Split(delimiterChars);
                                    xcounter = 1;
                                    for (x = 0; x <= (lineArray.Length - 1); x++)
                                    {
                                        if (lineArray[x] != "" && xcounter <= xmax)
                                        {
                                            double tval = double.Parse(lineArray[x]);
                                            if (tval != -9999)
                                            {
                                                water_temp[xcounter, y] = tval;
                                                water_temp_prev[xcounter, y] = tval;
                                            }
                                            xcounter++;
                                        }
                                    }
                                    y++;
                                }
                                sr.Close();

                                // TEMP_V1 - assign the constant initial value to any cell that starts wet
                                // (water_depth already loaded above) and wasn't already set by the raster.
                                // Dry / out-of-domain cells remain at the -9999 nodata sentinel until they
                                // first wet during the run (handled by the advection function, next step).
                                for (int xt = 1; xt <= xmax; xt++)
                                {
                                    for (int yt = 1; yt <= ymax; yt++)
                                    {
                                        if (elev[xt, yt] > -9999 && water_depth[xt, yt] > water_depth_erosion_threshold && water_temp[xt, yt] == -9999)
                                        {
                                            water_temp[xt, yt] = waterTempInitialValue;
                                            water_temp_prev[xt, yt] = waterTempInitialValue;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                MessageBox.Show("Initial water temperature raster file not found: " + FILE_NAME + ". Using the constant value instead.");
                            }
                        }
                        catch (Exception eTempRaster)
                        {
                            MessageBox.Show("Error loading the initial water temperature raster. Using the constant value instead." +
                                "\n\nDebug info: \n" + eTempRaster.Message + "\n\nStackTrace:\n" + eTempRaster.StackTrace);
                        }
                    }
                }
            
                if (isTraceSolutes == true)
                {
                    if (nSolutes == 0)
                    {
                        MessageBox.Show("Warning: No solutes were found in source files: solute tracing disabled.");
                        isTraceSolutes = false;
                        checkBoxSoluteTracer.Checked = false;
                    }
                    else
                    {
                        // MDW_V2: for solute tracing visualisation (normalised data), find max of inputs across each source
                        soluteMax = new double[nSolutes];
                        bool negativesolutes = false;
                        for (int solute = 0; solute < nSolutes; solute++)
                        {
                            soluteMax[solute] = 0.0;
                            for (int i = 0; i < inputfile.GetLength(0); i++)
                            {
                                for (int j = 0; j < inputfile.GetLength(1); j++)
                                {
                                    if (inputfile[i, j, solute + 14] < 0)
                                    {
                                        inputfile[i, j, solute + 14] = 0;
                                        negativesolutes = true;
                                    }
                                    if (soluteMax[solute] < inputfile[i, j, solute + 14])
                                    {
                                        soluteMax[solute] = inputfile[i, j, solute + 14];
                                    }
                                }

                            }
                            if (negativesolutes == true) MessageBox.Show("Warning: negative solute values found (they can only be zero or positive). Negative values have been changed to zero.");
                        }
                    }

                }
            }
            catch (Exception e)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + ". CAESAR will continue to function but may not be correct." +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message");

            }


            /* MDW_V2 - refactored code above
            try
            {
                if (inbox1.Checked)
                {
                    FILE_NAME = this.infile1.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[0, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //sourceIDs[1] = 1;
                        //inputfilenames.Add(FILE_NAME);
                    }

                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }

            try
            {
                if (inbox2.Checked)
                {
                    FILE_NAME = this.infile2.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[1, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[2] = inputfilenames.IndexOf(FILE_NAME) + 1;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }

            try
            {
                if (inbox3.Checked)
                {
                    FILE_NAME = this.infile3.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[2, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[3] = inputfilenames.IndexOf(FILE_NAME) + 1;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }
            try
            {
                if (inbox4.Checked)
                {
                    FILE_NAME = this.infile4.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[3, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[4] = inputfilenames.IndexOf(FILE_NAME) + 1;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }

            try
            {
                if (inbox5.Checked)
                {
                    FILE_NAME = this.infile5.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[4, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[5] = inputfilenames.IndexOf(FILE_NAME) + 1;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }
            try
            {

                if (inbox6.Checked)
                {
                    FILE_NAME = this.infile6.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[5, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[6] = inputfilenames.IndexOf(FILE_NAME) + 1;

                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }
            try
            {
                if (inbox7.Checked)
                {
                    FILE_NAME = this.infile7.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[6, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[7] = inputfilenames.IndexOf(FILE_NAME) + 1;

                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }

            try
            {
                if (inbox8.Checked)
                {
                    FILE_NAME = this.infile8.Text;
                    if (FILE_NAME != "null")
                    {
                        input_type_flag = 0;
                        StreamReader gr = File.OpenText(FILE_NAME);
                        while ((input = gr.ReadLine()) != null)
                        {
                            string[] lineArray;
                            lineArray = input.Split(delimiterChars);
                            for (x = 0; x <= (lineArray.Length - 1); x++)
                            {
                                if (lineArray[x] != "")
                                {
                                    tempx = int.Parse(lineArray[0]);
                                    inputfile[7, tempx, x] = double.Parse(lineArray[x]);
                                }
                            }
                        }
                        gr.Close();

                        // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used, not necessarily #1
                        nChecked++;
                        Array.Resize(ref sourceIDs, nChecked); // expand as needed
                        if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                        else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                        sourceIDs[nChecked - 1] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;
                        //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);
                        //sourceIDs[8] = inputfilenames.IndexOf(FILE_NAME) + 1;

                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }

            try //MDW_V2 : load extra sources
            {
                if (inboxExtra.Checked)
                {

                    //extraSourceFiles
                    for (int i = 0; i < extraSourceFiles.Length; i++)
                    {
                        FILE_NAME = extraSourceFiles[i];

                        if (FILE_NAME != "null")
                        {
                            input_type_flag = 0;
                            StreamReader gr = File.OpenText(FILE_NAME);
                            while ((input = gr.ReadLine()) != null)
                            {
                                string[] lineArray;
                                lineArray = input.Split(delimiterChars);
                                //MessageBox.Show("lineArray: " + lineArray);

                                for (x = 0; x <= (lineArray.Length - 1); x++)
                                {
                                    if (lineArray[x] != "")
                                    {
                                        tempx = int.Parse(lineArray[0]);
                                        inputfile[nChecked + i, tempx, x] = double.Parse(lineArray[x]);

                                    }
                                }
                            }
                            gr.Close();
                            //MessageBox.Show("Loaded extra source file: " + FILE_NAME + " (" + Convert.ToString(i+1) + " of " + extraSourceFiles.Length + ")");

                            // assign IDs for water source tracing - MDW_V2
                            Array.Resize(ref sourceIDs, nChecked + i + 1); // expand as needed
                            if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                            else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                            sourceIDs[nChecked + i] = inputfilenames.IndexOf(FILE_NAME) + 1 + sourceIndexAddition;



                            //if (!inputfilenames.Contains(FILE_NAME)) inputfilenames.Add(FILE_NAME);

                            //if (sourceIDs.Length < (nChecked + i))
                            //{
                            //    Array.Resize(ref sourceIDs, nChecked + extraSourceFiles.Length);
                            //}
                            //sourceIDs[nChecked + i] = inputfilenames.IndexOf(FILE_NAME) + 1;

                        }

                    }



                }
            }
            catch (Exception)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct");
            }
            */

            // code to try and draw in graphics from google maps etc...
            //string latlng = "39.24382,9.143372";
            //string path = "http://maps.googleapis.com/maps/api/staticmap?center=" + latlng +
            //   "&zoom=10&size=400x400&maptype=satellite&markers=color:blue%7Clabel:S%7C" +
            //   latlng + "&sensor=false";

            //try
            //{
            //    // bing maps instructions here: http://msdn.microsoft.com/en-us/library/ff701724.aspx
            //    // example line: http://dev.virtualearth.net/REST/v1/Imagery/Map/imagerySet?mapArea=mapArea&mapSize=mapSize&pushpin=pushpin&mapLayer=mapLayer&format=format&mapMetadata=mapMetadata&key=BingMapsKey

            //    point testPoint = new point(xll, yll);
            //    if (UTMgridcheckbox.Checked)
            //    {
            //        testPoint.UTMzone = System.Convert.ToInt32(UTMzonebox.Text);
            //        testPoint.south = System.Convert.ToBoolean(UTMsouthcheck.Checked);
            //        testPoint.transformUTMPoint();
            //    }
            //    else
            //    {
            //        testPoint.transformPoint();
            //    }
            //    yurcorner = yll + (System.Convert.ToDouble(ymax) * System.Convert.ToDouble(DX));
            //    xurcorner = xll + (System.Convert.ToDouble(xmax) * System.Convert.ToDouble(DX));
            //    point testPoint2 = new point(xurcorner, yurcorner);
            //    if (UTMgridcheckbox.Checked)
            //    {
            //        testPoint2.UTMzone = System.Convert.ToInt32(UTMzonebox.Text);
            //        testPoint2.south = System.Convert.ToBoolean(UTMsouthcheck.Checked);
            //        testPoint2.transformUTMPoint();
            //    }
            //    else
            //    {
            //        testPoint2.transformPoint();
            //    }

            //    string box = testPoint.ycoord + "," + testPoint.xcoord + "," + testPoint2.ycoord + "," + testPoint2.xcoord;
            //    //string box = "54.3397646119184,-2.30842643099455,54.4619251486287,-1.95376438512343";
            //    string path = "http://dev.virtualearth.net/REST/v1/Imagery/Map/Aerial?mapArea=" + box + "&mapSize=" + xmax + "," + ymax + "&key=AvaXZOU-oYf4A4vjryoLQEv06rQy9V3BBJkb7Bi4v8HaXoUTdDZevDbqtFEe9mi_";

            //    using (WebClient wc = new WebClient())
            //    {
            //        wc.DownloadFile(path, @"img.png");
            //    }
            //}
            //catch
            //{
            //    MessageBox.Show("Time out trying to download background image: Check web connection");
            //}


            // MessageBox.Show(Convert.ToString(input_type_flag));
            this.InfoStatusPanel.Text = "Loaded: type = " + input_type_flag.ToString();        // MJ 02/02/05

        }

        void load_hydrofile(string FILE_NAME, char[] delimiterChars, int inputIndex) // MDW_V2 moved here to faciliate easier modification
        {
            input_type_flag = 0; // is this still needed? appears to be unused except for a message
            int tempx = 0;
            string input;
            int inputfilewidth = inputfile.GetLength(2);
            int inputfileStandardWidth = 14;
            int max_file_length = (int)((maxcycle * 60) / input_time_step) + 10; // MDW_Apr24: moved as used in check below
            char commentline = '#';

            try
            {
                StreamReader gr = File.OpenText(FILE_NAME);
                while ((input = gr.ReadLine()) != null && tempx < max_file_length - 1) // MDW_Apr24: prevent overrun if a longer file than required is used
                {
                    if (input.Length == 0) { continue; } // skip empty lines
                    if (input[0].CompareTo(commentline) == 0) { continue; } // skip commented lines

                    string[] lineArray;
                    lineArray = input.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries); // MDW_V2 option to handle multiple consecutive delimeters


                    if (isTraceSolutes == true) // expand grid as needed to add solutes
                    {
                        if (lineArray.Length > inputfilewidth) // MDW_Apr24: corrected check so that it only triggers once per additional solute
                        {
                            inputfile = Resize3DdoubleArray(inputfile,
                                                            new int[] { number_of_points, max_file_length,
                                                            lineArray.Length });
                            inputfilewidth = inputfile.GetLength(2);
                            nSolutes = Math.Max(lineArray.Length - inputfileStandardWidth, nSolutes);
                        }
                    }

                    for (int x = 0; x < Math.Min(lineArray.Length, inputfilewidth); x++) // this check will avoid the overflow caused when
                                                                                         // solute tracing is off but a solute hydro input file is used
                    {
                        if (lineArray[x] != "")
                        {
                            tempx = int.Parse(lineArray[0]);
                            inputfile[inputIndex, tempx, x] = double.Parse(lineArray[x]);
                        }
                    }


                }
                gr.Close();

                // assign IDs for water source tracing - MDW_V2: modified to enable any checkboxes to be used
                Array.Resize(ref sourceIDs, sourceIDs.Length + 1); // expand as needed
                if (inputfilenames.Count == 0) { inputfilenames.Add(FILE_NAME); }
                else if (!inputfilenames.Contains(FILE_NAME)) { inputfilenames.Add(FILE_NAME); }
                sourceIDs[inputIndex] = inputfilenames.IndexOf(FILE_NAME) + sourceIndexAddition;



            }
            catch (Exception e)
            {
                MessageBox.Show("There was some type of error loading the input data from " + FILE_NAME + " at line " + Convert.ToString(tempx) + ", CAESAR will continue to function but may not be correct" +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message"
            }

        }


        // TEMP_V1 - loads a text time-series met file into the corresponding hourly_* array
        // (mirrors load_hydrofile()). One value per zone per row; no leading index column.
        // Rows are zero-indexed (row 0 = first time step) - NOTE this differs from the
        // 1-indexed convention used by hourly_rain_data/hourly_m_value.
        void load_met_file(string FILE_NAME, char[] delimiterChars, double[,] targetArray)
        {
            if (FILE_NAME == "null" || !File.Exists(FILE_NAME))
            {
                MessageBox.Show("Meteorological input file not found: " + FILE_NAME + ". Water temperature simulation may not be correct.");
                return;
            }

            char commentline = '#';
            int nZones = targetArray.GetLength(1);
            int max_file_length = targetArray.GetLength(0);
            int inc = 0;
            string input;

            try
            {
                StreamReader gr = File.OpenText(FILE_NAME);
                while ((input = gr.ReadLine()) != null && inc < max_file_length - 1)
                {
                    if (input.Length == 0) { continue; } // skip empty lines
                    if (input[0].CompareTo(commentline) == 0) { continue; } // skip commented lines

                    string[] lineArray = input.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                    int xcounter = 0;
                    for (int x = 0; x <= (lineArray.Length - 1); x++)
                    {
                        if (xcounter >= nZones) continue; // ignore extra columns beyond the number of met zones in use
                        if (lineArray[x] != "")
                        {
                            targetArray[inc, xcounter] = double.Parse(lineArray[x]);
                            xcounter++;
                        }
                    }
                    inc++;
                }
                gr.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show("There was some type of error loading the meteorological input data from " + FILE_NAME + " at line " + Convert.ToString(inc) + ". CAESAR will continue to function but water temperature may not be correct." +
                    "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace);
            }
        }

        // TEMP_V1 - loads the shared rain/tide/reach source temperature file, parses the
        // flexible header row (single index / a-b range / a+b combination / ALL), gap-fills
        // each column (interpolate internal gaps, hold flat beyond the first/last valid
        // value), and reports the resulting source-index-to-column mapping.
        void load_source_temp_file(string FILE_NAME, char[] delimiterChars)
        {
            sourceTempColumnFor = new int[nSources];
            for (int i = 0; i < nSources; i++) sourceTempColumnFor[i] = -1;
            sourceTempData = null;
            nSourceTempColumns = 0;

            if (FILE_NAME == "null" || !File.Exists(FILE_NAME))
            {
                return; // optional file - every source falls back to waterTempInitialValue
            }

            char commentline = '#';
            string mappingReport = "";

            try
            {
                StreamReader gr = File.OpenText(FILE_NAME);
                string input;
                string headerLine = null;

                while ((input = gr.ReadLine()) != null)
                {
                    if (input.Length == 0) continue;
                    if (input[0].CompareTo(commentline) == 0) continue;
                    headerLine = input;
                    break;
                }

                if (headerLine == null)
                {
                    MessageBox.Show("Source temperature file " + FILE_NAME + " has no header row. File ignored; all sources will use the background initial temperature.");
                    gr.Close();
                    return;
                }

                string[] headerTokens = headerLine.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                int nCols = headerTokens.Length;
                int allColumn = -1;
                bool[] indexClaimed = new bool[nSources];

                for (int col = 0; col < nCols; col++)
                {
                    string token = headerTokens[col].Trim();
                    if (token.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                    {
                        if (allColumn == -1)
                        {
                            allColumn = col;
                        }
                        else
                        {
                            mappingReport += "Warning: more than one ALL column found; first occurrence (column " + (allColumn + 1) + ") kept, column " + (col + 1) + " ignored.\n";
                        }
                        continue;
                    }

                    string[] parts = token.Split('+');
                    foreach (string part in parts)
                    {
                        int lo, hi;
                        if (part.Contains("-"))
                        {
                            string[] range = part.Split('-');
                            lo = int.Parse(range[0]);
                            hi = int.Parse(range[1]);
                        }
                        else
                        {
                            lo = hi = int.Parse(part);
                        }

                        for (int idx = lo; idx <= hi; idx++)
                        {
                            if (idx < 0 || idx >= nSources)
                            {
                                mappingReport += "Warning: header refers to source index " + idx + ", outside the valid range (0-" + (nSources - 1) + "). Ignored.\n";
                                continue;
                            }
                            if (indexClaimed[idx])
                            {
                                mappingReport += "Warning: source index " + idx + " is claimed by more than one column; first assignment kept, column " + (col + 1) + " ignored for this index.\n";
                                continue;
                            }
                            indexClaimed[idx] = true;
                            sourceTempColumnFor[idx] = col;
                        }
                    }
                }

                for (int idx = 0; idx < nSources; idx++)
                {
                    if (sourceTempColumnFor[idx] == -1 && allColumn != -1)
                    {
                        sourceTempColumnFor[idx] = allColumn;
                    }
                }

                int max_file_length = (int)((maxcycle * 60) / met_data_time_step) + 10;
                double[,] rawData = new double[max_file_length, nCols];
                for (int r = 0; r < max_file_length; r++)
                    for (int c = 0; c < nCols; c++)
                        rawData[r, c] = -9999;

                int rowIdx = 0;
                while ((input = gr.ReadLine()) != null && rowIdx < max_file_length)
                {
                    if (input.Length == 0) continue;
                    if (input[0].CompareTo(commentline) == 0) continue;

                    string[] lineArray = input.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);
                    for (int c = 0; c < Math.Min(lineArray.Length, nCols); c++)
                    {
                        if (lineArray[c] != "") rawData[rowIdx, c] = double.Parse(lineArray[c]);
                    }
                    rowIdx++;
                }
                gr.Close();

                // gap-fill: interpolate internal -9999 runs between bracketing valid values;
                // hold flat beyond the first/last valid value; leave whole column at -9999
                // if it has no valid data at all (handled as "no data" at lookup time)
                for (int c = 0; c < nCols; c++)
                {
                    int firstValid = -1, lastValid = -1;
                    for (int r = 0; r < rowIdx; r++)
                    {
                        if (rawData[r, c] != -9999)
                        {
                            if (firstValid == -1) firstValid = r;
                            lastValid = r;
                        }
                    }
                    if (firstValid == -1) continue;

                    for (int r = 0; r < firstValid; r++) rawData[r, c] = rawData[firstValid, c];
                    for (int r = lastValid + 1; r < max_file_length; r++) rawData[r, c] = rawData[lastValid, c]; // TEMP_V1 - hold flat for the rest of the run, not just to the end of the file's actual rows

                    int prevValid = firstValid;
                    for (int r = firstValid + 1; r <= lastValid; r++)
                    {
                        if (rawData[r, c] != -9999) { prevValid = r; continue; }
                        int nextValid = r;
                        while (rawData[nextValid, c] == -9999) nextValid++;
                        double frac = (double)(r - prevValid) / (double)(nextValid - prevValid);
                        rawData[r, c] = rawData[prevValid, c] + frac * (rawData[nextValid, c] - rawData[prevValid, c]);
                    }
                }

                sourceTempData = rawData;
                nSourceTempColumns = nCols;
            }
            catch (Exception eSourceTemp)
            {
                MessageBox.Show("Error loading the source temperature file " + FILE_NAME + ". All sources will use the background initial temperature." +
                    "\n\nDebug info: \n" + eSourceTemp.Message + "\n\nStackTrace:\n" + eSourceTemp.StackTrace);
                sourceTempData = null;
                nSourceTempColumns = 0;
                return;
            }

            // Diagnostic: full source index -> name -> column/default mapping, plus any warnings above
            mappingReport += "\nSource temperature file loaded: " + FILE_NAME + "\n\nSource index mapping:\n";
            for (int idx = 0; idx < nSources; idx++)
            {
                string srcName;
                if (idx == 0) srcName = "Stage" + (checkBox3.Checked ? "" : " [not active]");
                else if (idx == 1) srcName = "Rain" + (catchment_mode_box.Checked ? "" : " [not active]");
                else srcName = (idx - sourceIndexAddition < inputfilenames.Count) ? inputfilenames[idx - sourceIndexAddition] : "(unknown source)";

                if (sourceTempColumnFor[idx] == -1)
                    mappingReport += "  " + idx + " : " + srcName + " -> no column found, using background initial value (" + waterTempInitialValue + " C)\n";
                else
                    mappingReport += "  " + idx + " : " + srcName + " -> column " + (sourceTempColumnFor[idx] + 1) + "\n";
            }
            MessageBox.Show(mappingReport);
        }

        // TEMP_V1 - time-interpolated lookup of a source's temperature at the current cycle.
        // Falls back to waterTempInitialValue if the source has no mapped column, or the
        // column had no valid data anywhere in the file.
        double get_source_temperature(int sourceIndex, double cycleMinutes)
        {
            if (sourceTempData == null || sourceIndex < 0 || sourceIndex >= nSources) return waterTempInitialValue;
            int col = sourceTempColumnFor[sourceIndex];
            if (col == -1) return waterTempInitialValue;

            int maxIdx = sourceTempData.GetLength(0) - 1;
            int idx0 = (int)(cycleMinutes / met_data_time_step);
            if (idx0 < 0) idx0 = 0;
            if (idx0 >= maxIdx) idx0 = maxIdx - 1;
            if (idx0 < 0) idx0 = 0;
            int idx1 = idx0 + 1;
            if (idx1 > maxIdx) idx1 = maxIdx;

            double v0 = sourceTempData[idx0, col];
            double v1 = sourceTempData[idx1, col];
            if (v0 == -9999 || v1 == -9999) return waterTempInitialValue; // column had no valid data at all

            double proportion = (((idx0 + 1) * met_data_time_step) - cycleMinutes) / met_data_time_step;
            return v0 + ((v1 - v0) * (1 - proportion));
        }

        void save_data(int typeflag, double tempcycle)
        {
            int x, y, z, inc, nn;

            string FILENAME = "waterdepth.dat";
            string FILENAME1 = "";

            if (uniquefilecheck.Checked == false) tempcycle = 0;

            // turns file name into days from mins.



            if (typeflag == 1 && tempcycle == 0) FILENAME = "waterdepth.asc";
            if (typeflag == 2 && tempcycle == 0) FILENAME = "elevdiff.asc";
            if (typeflag == 3 && tempcycle == 0) FILENAME = "elev.asc";
            if (typeflag == 4 && tempcycle == 0) FILENAME = "grain.txt";
            if (typeflag == 15 && tempcycle == 0) FILENAME = "d50top.asc";
            if (typeflag == 16 && tempcycle == 0) FILENAME = "velocity.asc";			// <JOE 20050605>
            if (typeflag == 17 && tempcycle == 0) FILENAME = "velocity_vectors.txt";    // <JOE 20050605>
            if (typeflag == 18 && tempcycle == 0) FILENAME = "watersource"; // MDW 17-03-2016
            if (typeflag == 19 && tempcycle == 0) FILENAME = "watersource_rainzone"; // MDW 13-04-2016 // MDW_V2
            if (typeflag == 20 && tempcycle == 0) FILENAME = "watersource_solute"; // MDW_V2
            if (typeflag == 6 && tempcycle == 0) FILENAME = "tracer.asc";
            if (typeflag == 7 && tempcycle == 0) FILENAME = "oildepth.asc"; // OIL_V1_PDF
            if (typeflag == 8) FILENAME = "massbalance.txt"; // OIL_V1_PDF
            if (typeflag == 9) FILENAME = "oilconcentration.asc"; // OIL_V1_PDF



            if (typeflag == 1 && tempcycle > 0) FILENAME = "waterdepth" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc";
            if (typeflag == 2 && tempcycle > 0) FILENAME = "elevdiff" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc";
            if (typeflag == 3 && tempcycle > 0) FILENAME = "elev.dat" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc";
            if (typeflag == 4 && tempcycle > 0) FILENAME = "grain.dat" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".txt";

            if (typeflag == 15 && tempcycle > 0) FILENAME = "d50top" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc";
            if (typeflag == 16 && tempcycle > 0) FILENAME = "velocity" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc";
            if (typeflag == 17 && tempcycle > 0) FILENAME = "velocity_vectors" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".txt";
            if (typeflag == 18 && tempcycle > 0) FILENAME = "watersource" + Convert.ToString(Convert.ToInt64(tempcycle)); // MDW
            if (typeflag == 19 && tempcycle > 0) FILENAME = "watersource" + Convert.ToString(Convert.ToInt64(tempcycle)) + "_rainzone"; //MDW_V2
            if (typeflag == 20 && tempcycle > 0) FILENAME = "watersource" + Convert.ToString(Convert.ToInt64(tempcycle)) + "_solute"; //MDW_V2
            if (typeflag == 6 && tempcycle > 0) FILENAME = "tracer" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc";
            if (typeflag == 7 && tempcycle > 0) FILENAME = "oildepth" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc"; //OIL_V1_PDF
            //if (typeflag == 8 && tempcycle > 0) FILENAME = "massbalance" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".txt"; //OIL_V1_PDF
            if (typeflag == 9 && tempcycle > 0) FILENAME = "oilconcentration" + Convert.ToString(Convert.ToInt64(tempcycle)) + ".asc"; //OIL_V1_PDF



            // MDW_V2 add folder to filename string here
            if (string.IsNullOrEmpty(outdir) == false) FILENAME = Path.Combine(outdir, FILENAME);

            if (typeflag >= 1 && typeflag < 4)
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    sw.Write("ncols         " + xmax);
                    sw.Write("\n");
                    sw.Write("nrows         " + ymax);
                    sw.Write("\n");

                    for (nn = 2; nn <= 5; nn++)
                    {
                        sw.Write(inputheader[nn]); sw.Write("\n");
                    }

                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {
                            if (typeflag == 1)
                            {
                                //sw.Write(area[x, y]); sw.Write(" ");
                                if (water_depth[x, y] > 0)
                                {
                                    //sw.Write(vel_dir[x,y,0]);
                                    sw.Write(water_depth[x, y]);

                                    sw.Write(" ");
                                }
                                else
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }
                            }

                            if (typeflag == 2)
                            {
                                sw.Write(init_elevs[x, y] - elev[x, y]);
                                sw.Write(" ");
                            }
                            if (typeflag == 3)
                            {
                                sw.Write(elev[x, y]);
                                sw.Write(" ");
                            }
                        }
                        sw.Write("\n");
                    }


                }

            }

            if (typeflag == 4)
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {


                            if (index[x, y] != -9999)
                            {
                                sw.Write(x); sw.Write(" "); sw.Write(y); sw.Write(" "); sw.Write(index[x, y]); sw.Write(" ");
                                for (inc = 0; inc <= G_MAX; inc++)
                                {
                                    sw.Write(grain[index[x, y], inc, 0]); sw.Write(" ");
                                }

                                for (z = 0; z <= 9; z++)
                                {
                                    for (inc = 0; inc <= (G_MAX - 2); inc++)
                                    {
                                        sw.Write(strata[index[x, y], z, inc, 0]); sw.Write(" ");
                                    }
                                }
                                sw.Write("\n");
                            }
                        }
                    }
                }
            }


            if (typeflag == 15)
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    for (nn = 0; nn <= 5; nn++)
                    {
                        sw.Write(inputheader[nn]); sw.Write("\n");
                    }

                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {
                            if (index[x, y] == -9999)
                            {
                                sw.Write("-9999.0");
                                sw.Write(" ");
                            }
                            else
                            {
                                sw.Write(d50(index[x, y]));
                                sw.Write(" ");
                            }
                        }
                        sw.Write("\n");
                    }

                }
            }

            //save tracer file

            if (typeflag == 6)
            {
                for (int T = 0; T <= tracers; T++)
                {
                    FILENAME1 = Convert.ToString(T) + FILENAME;
                    using (StreamWriter sw = new StreamWriter(FILENAME1))
                    {
                        for (nn = 0; nn <= 5; nn++)
                        {
                            sw.Write(inputheader[nn]); sw.Write("\n");
                        }

                        for (y = 1; y <= ymax; y++)
                        {
                            for (x = 1; x <= xmax; x++)
                            {
                                if (index[x, y] == -9999)
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }
                                else
                                {
                                    double temptot = 0;
                                    for (int i = 0; i <= G_MAX; i++)
                                    {
                                        temptot += grain[index[x, y], i, T];
                                    }
                                    for (int rr = 0; rr <= 9; rr++)
                                    {
                                        for (inc = 0; inc <= (G_MAX - 2); inc++)
                                        {
                                            temptot += strata[index[x, y], rr, inc, T];
                                        }
                                    }
                                    sw.Write(temptot);
                                    sw.Write(" ");
                                }
                            }
                            sw.Write("\n");
                        }
                    }
                }
            }

            if (typeflag == 16)   // <JOE 20050605> Write velocity to a file
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    for (nn = 0; nn <= 5; nn++)
                    {
                        sw.Write(inputheader[nn]); sw.Write("\n");
                    }

                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {
                            if (water_depth[x, y] < water_depth_erosion_threshold)
                            {
                                sw.Write("-9999.0");
                                sw.Write(" ");
                            }
                            else
                            {
                                sw.Write(Vel[x, y]);
                                sw.Write(" ");
                            }
                        }
                        sw.Write("\n");
                    }

                }
            }

            if (typeflag == 17)   // <JOE 20050605> Write soil_saturation to a file
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    //for (nn=0; nn<=5; nn++)
                    //{
                    //    sw.Write(inputheader[nn]);sw.Write("\n");
                    //}

                    sw.Write("x,y,rot,vel");
                    sw.Write("\n");

                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {
                            if (water_depth[x, y] > water_depth_erosion_threshold)
                            {
                                sw.Write((xll + (x * DX)) + (DX / 2)); sw.Write(","); sw.Write(((yll + ((ymax - y) * DX)))); sw.Write(","); sw.Write(calc_max_flow_direction_degrees(x, y)); sw.Write(","); sw.Write(Vel[x, y]);
                                sw.Write("\n");
                            }

                        }

                    }

                }
            }


            // write out water source tracers - MDW
            if (typeflag == 18)
            {
                string FILENAMESRC;

                // go through each water source
                for (int src = 0; src < nSources; src++) // MDW_V2 updated to zero-index
                {


                    if (src == 0) // MDW_V2: stage now in the zero index
                    {
                        if (checkBox3.Checked == false) { continue; }
                        FILENAMESRC = FILENAME + "_Stage.txt";
                    }
                    else if (src == 1)
                    {
                        if (catchment_mode_box.Checked == false) { continue; }
                        FILENAMESRC = FILENAME + "_Rain.txt";
                    }
                    else
                    {
                        FILENAMESRC = FILENAME + "_Hydro" + Convert.ToString(Convert.ToInt64(src - 1)) + ".txt";
                    }

                    using (StreamWriter sw = new StreamWriter(FILENAMESRC))
                    {
                        sw.Write("ncols         " + xmax);
                        sw.Write("\n");
                        sw.Write("nrows         " + ymax);
                        sw.Write("\n");

                        for (nn = 2; nn <= 5; nn++)
                        {
                            sw.Write(inputheader[nn]); sw.Write("\n");
                        }

                        for (y = 1; y <= ymax; y++)
                        {
                            for (x = 1; x <= xmax; x++)
                            {
                                if (water_depth[x, y] > 0)
                                {
                                    sw.Write(Math.Round(watertracer[x, y, src], 6)); // MDW_Apr24: increased precision of output
                                    sw.Write(" ");
                                }
                                else
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }

                            }
                            sw.Write("\n");
                        }
                    }
                }
            }
            // write out rain zone water source tracers - MDW
            if (typeflag == 19)
            {
                string FILENAMESRC;

                // go through each rain zone
                for (int src = 0; src < nRainZones; src++)
                {
                    FILENAMESRC = FILENAME + "_zone" + Convert.ToString(Convert.ToInt64(rainZones[src])) + ".txt"; // MDW_V2 fixed zone reference

                    using (StreamWriter sw = new StreamWriter(FILENAMESRC))
                    {
                        sw.Write("ncols         " + xmax);
                        sw.Write("\n");
                        sw.Write("nrows         " + ymax);
                        sw.Write("\n");

                        for (nn = 2; nn <= 5; nn++)
                        {
                            sw.Write(inputheader[nn]); sw.Write("\n");
                        }

                        for (y = 1; y <= ymax; y++)
                        {
                            for (x = 1; x <= xmax; x++)
                            {
                                if (water_depth[x, y] > 0)
                                {
                                    sw.Write(Math.Round(watertracerRainZone[x, y, src], 6)); // MDW_Apr24: increased precision of output
                                    sw.Write(" ");
                                }
                                else
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }

                            }
                            sw.Write("\n");
                        }
                    }
                }

            }
            // write out solute tracers - MDW_V2
            if (typeflag == 20)
            {
                string FILENAMESRC;

                // go through each water source
                for (int src = 0; src < nSolutes; src++)
                {
                    FILENAMESRC = FILENAME + Convert.ToString(Convert.ToInt64(src + 1)) + ".txt";

                    using (StreamWriter sw = new StreamWriter(FILENAMESRC))
                    {
                        sw.Write("ncols         " + xmax);
                        sw.Write("\n");
                        sw.Write("nrows         " + ymax);
                        sw.Write("\n");

                        for (nn = 2; nn <= 5; nn++)
                        {
                            sw.Write(inputheader[nn]); sw.Write("\n");
                        }

                        for (y = 1; y <= ymax; y++)
                        {
                            for (x = 1; x <= xmax; x++)
                            {
                                if (water_depth[x, y] > 0)
                                {
                                    sw.Write(solutetracer[x, y, src]); // MDW_Apr24: increased to full precision (inputs may be very small numbers)
                                    sw.Write(" ");
                                }
                                else
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }

                            }
                            sw.Write("\n");
                        }
                    }
                }

            }
            if (typeflag == 7 && isOilSimulation == true)  //OIL_V1_PDF
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    sw.Write("ncols         " + xmax);
                    sw.Write("\n");
                    sw.Write("nrows         " + ymax);
                    sw.Write("\n");

                    for (nn = 2; nn <= 5; nn++)
                    {
                        sw.Write(inputheader[nn]); sw.Write("\n");
                    }

                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {
                            if (typeflag == 7)
                            {

                                if (oil_depth[x, y] > 0)
                                {

                                    sw.Write(oil_depth[x, y]);

                                    sw.Write(" ");
                                }
                                else
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }
                            }
                        }
                    }

                }
            }

            if (typeflag == 8 && isOilSimulation == true)  //OIL_V1_PDF
            {

                if (oil_in_sum > 0)
                {
                    using (StreamWriter sw = new StreamWriter(FILENAME, append: true))
                    {
                        sw.WriteLine($"{save_time}, {oil_in_sum}, {Vout_total}, {oil_evap_sum}, {error_negative}, {oil_S},    {Ev}");
                    }
                }
            }

            if (typeflag == 9 && isOilSimulation == true)  //OIL_V1_PDF
            {

                using (StreamWriter sw = new StreamWriter(FILENAME))
                {
                    sw.Write("ncols         " + xmax);
                    sw.Write("\n");
                    sw.Write("nrows         " + ymax);
                    sw.Write("\n");

                    for (nn = 2; nn <= 5; nn++)
                    {
                        sw.Write(inputheader[nn]); sw.Write("\n");
                    }

                    for (y = 1; y <= ymax; y++)
                    {
                        for (x = 1; x <= xmax; x++)
                        {
                            if (typeflag == 9)
                            {

                                if (oil_concentration[x, y] > 0)
                                {

                                    sw.Write(oil_concentration[x, y]);

                                    sw.Write(" ");
                                }
                                else
                                {
                                    sw.Write("-9999.0");
                                    sw.Write(" ");
                                }
                            }
                        }
                    }

                }
            }
        }

        private void initialise()
        {

            this.InfoStatusPanel.Text = "Initialising..";           // MJ 14/01/05

            if (overrideheaderBox.Checked == true)
            {
                // input xmax, ymax and DX from text boxes
                xmax = int.Parse(xtextbox.Text);
                ymax = int.Parse(ytextbox.Text);
                DX = double.Parse(dxbox.Text);             // now in read_header()  MJ 24/05/05
            }


            save_interval2 = int.Parse(googAnimationSaveInterval.Text);
            save_time2 = save_interval2;

            in_out_difference = double.Parse(initscansbox.Text);
            ERODEFACTOR = double.Parse(erodefactorbox.Text);
            root = (Math.Sqrt(Math.Pow(DX, 2) + Math.Pow(DX, 2)));
            LIMIT = int.Parse(limitbox.Text);
            MIN_Q = double.Parse(minqbox.Text);
            CREEP_RATE = double.Parse(creepratebox.Text);
            SOIL_RATE = double.Parse(soil_ratebox.Text);
            bed_proportion = double.Parse(lateralratebox.Text);

            cycle = double.Parse(textBox1.Text) * 60;

            maxcycle = int.Parse(cyclemaxbox.Text);
            failureangle = double.Parse(slopebox.Text);

            saveinterval = double.Parse(saveintervalbox.Text);
            //M=double.Parse(mvaluebox.Text);
            grow_grass_time = double.Parse(grasstextbox.Text);
            output_file_save_interval = int.Parse(outputfilesaveintervalbox.Text);
            tx = output_file_save_interval;
            min_time_step = double.Parse(mintimestepbox.Text);
            active = double.Parse(activebox.Text);
            k_evap = double.Parse(k_evapBox.Text);  // added MJ 15/03/05
            vegTauCrit = double.Parse(vegTauCritBox.Text);  // added MJ 10/05/05
            max_time_step = int.Parse(max_time_step_Box.Text);
            water_depth_erosion_threshold = double.Parse(Q2box.Text);
            max_vel = double.Parse(max_vel_box.Text);
            courant_number = double.Parse(courantbox.Text);
            hflow_threshold = double.Parse(textBox4.Text);
            lateral_cross_channel_smoothing = double.Parse(textBox7.Text);
            froude_limit = double.Parse(textBox8.Text);
            mannings = double.Parse(textBox9.Text);

            rfnum = int.Parse(rfnumBox.Text);
            tracers = int.Parse(tracer_num.Text);

            // MDW_V2 - variables needed for loading extra source file
            String input, fExtra;
            char[] delimiterChars = { ' ', ',', '\t' };
            char commentline = '#';
            int inc = 0, nExtra = 0, xExtra = 0, yExtra = 0;

            if (googleAnimationCheckbox.Checked)
            {
                startDate = googleBeginDate.Text;
                // MDW_V2 added code to handle exception caused by empty string in googleBeginDate.Text
                // if not specified, will set start at today's date
                if (string.IsNullOrEmpty(startDate) == true)
                {
                    DateTime currentDateTime = DateTime.Now;
                    startDate = currentDateTime.ToString("yyyy-MM-dd");
                }
                googleTime = System.DateTime.Parse(startDate);
            }
            d1 = double.Parse(g1box.Text);
            d2 = double.Parse(g2box.Text);
            d3 = double.Parse(g3box.Text);
            d4 = double.Parse(g4box.Text);
            d5 = double.Parse(g5box.Text);
            d6 = double.Parse(g6box.Text);
            d7 = double.Parse(g7box.Text);
            d8 = double.Parse(g8box.Text);
            d9 = double.Parse(g9box.Text);

            // particle size distribution 2
            d1_ = double.Parse(g1_box.Text);
            d2_ = double.Parse(g2_box.Text);
            d3_ = double.Parse(g3_box.Text);
            d4_ = double.Parse(g4_box.Text);
            d5_ = double.Parse(g5_box.Text);
            d6_ = double.Parse(g6_box.Text);
            d7_ = double.Parse(g7_box.Text);
            d8_ = double.Parse(g8_box.Text);
            d9_ = double.Parse(g9_box.Text);

            // particle size distribution 1
            dprop = new double[11];

            dprop[1] = double.Parse(gp1box.Text);
            dprop[2] = double.Parse(gp2box.Text);
            dprop[3] = double.Parse(gp3box.Text);
            dprop[4] = double.Parse(gp4box.Text);
            dprop[5] = double.Parse(gp5box.Text);
            dprop[6] = double.Parse(gp6box.Text);
            dprop[7] = double.Parse(gp7box.Text);
            dprop[8] = double.Parse(gp8box.Text);
            dprop[9] = double.Parse(gp9box.Text);

            // particle size distribution 2 (landslide)
            dprop_ = new double[11];

            dprop_[1] = double.Parse(gp1_box.Text);
            dprop_[2] = double.Parse(gp2_box.Text);
            dprop_[3] = double.Parse(gp3_box.Text);
            dprop_[4] = double.Parse(gp4_box.Text);
            dprop_[5] = double.Parse(gp5_box.Text);
            dprop_[6] = double.Parse(gp6_box.Text);
            dprop_[7] = double.Parse(gp7_box.Text);
            dprop_[8] = double.Parse(gp8_box.Text);
            dprop_[9] = double.Parse(gp9_box.Text);


            G_MAX = 10;
            if (dprop[9] == 0) G_MAX = 9;
            if (dprop[8] == 0) G_MAX = 8;
            if (dprop[7] == 0) G_MAX = 7;
            if (dprop[6] == 0) G_MAX = 6;
            if (dprop[5] == 0) G_MAX = 5;
            if (dprop[4] == 0) G_MAX = 4;
            if (dprop[3] == 0) G_MAX = 3;
            if (dprop[2] == 0) G_MAX = 2;
            if (checkBox8.Checked == true) G_MAX = 10;


            // new array to deal with suspended sediment   MJ 09/05/05
            isSuspended = new bool[10 + 1];
            isSuspended[1] = suspGS1box.Checked;
            isSuspended[2] = false;
            isSuspended[3] = false;
            isSuspended[4] = false;
            isSuspended[5] = false;
            isSuspended[6] = false;
            isSuspended[7] = false;
            isSuspended[8] = false;
            isSuspended[9] = false;

            // new array to deal with suspended sediment   MJ 09/05/05
            fallVelocity = new double[10 + 1];
            fallVelocity[1] = double.Parse(fallGS1box.Text);
            fallVelocity[2] = double.Parse(fallGS2box.Text);
            fallVelocity[3] = double.Parse(fallGS3box.Text);
            fallVelocity[4] = double.Parse(fallGS4box.Text);
            fallVelocity[5] = double.Parse(fallGS5box.Text);
            fallVelocity[6] = double.Parse(fallGS6box.Text);
            fallVelocity[7] = double.Parse(fallGS7box.Text);
            fallVelocity[8] = double.Parse(fallGS8box.Text);
            fallVelocity[9] = double.Parse(fallGS9box.Text);

            input_time_step = int.Parse(input_time_step_box.Text);
            stage_input_time_step = double.Parse(TidalInputStep.Text);
            edgeslope = double.Parse(textBox2.Text);
            lateral_constant = double.Parse(textBox3.Text);
            veg_lat_restriction = double.Parse(veg_lat_box.Text);

            bedrock_erosion_rate = double.Parse(bedrock_erosion_rate_box.Text);
            bedrock_erosion_threshold = double.Parse(bedrock_erosion_threshold_box.Text);

            inpoints = new int[10, 2];
            inputpointsarray = new bool[xmax + 2, ymax + 2];

            // then intiailise the main arrays
            if (inbox1.Checked)
            {
                number_of_points++;
                inpoints[0, 0] = int.Parse(xbox1.Text);
                inpoints[0, 1] = int.Parse(ybox1.Text);
                inputpointsarray[int.Parse(xbox1.Text), int.Parse(ybox1.Text)] = true;
            }

            if (inbox2.Checked)
            {
                number_of_points++;
                inpoints[1, 0] = int.Parse(xbox2.Text);
                inpoints[1, 1] = int.Parse(ybox2.Text);
                inputpointsarray[int.Parse(xbox2.Text), int.Parse(ybox2.Text)] = true;
            }

            if (inbox3.Checked)
            {
                number_of_points++;
                inpoints[2, 0] = int.Parse(xbox3.Text);
                inpoints[2, 1] = int.Parse(ybox3.Text);
                inputpointsarray[int.Parse(xbox3.Text), int.Parse(ybox3.Text)] = true;
            }

            if (inbox4.Checked)
            {
                number_of_points++;
                inpoints[3, 0] = int.Parse(xbox4.Text);
                inpoints[3, 1] = int.Parse(ybox4.Text);
                inputpointsarray[int.Parse(xbox4.Text), int.Parse(ybox4.Text)] = true;
            }

            if (inbox5.Checked)
            {
                number_of_points++;
                inpoints[4, 0] = int.Parse(xbox5.Text);
                inpoints[4, 1] = int.Parse(ybox5.Text);
                inputpointsarray[int.Parse(xbox5.Text), int.Parse(ybox5.Text)] = true;
            }

            if (inbox6.Checked)
            {
                number_of_points++;
                inpoints[5, 0] = int.Parse(xbox6.Text);
                inpoints[5, 1] = int.Parse(ybox6.Text);
                inputpointsarray[int.Parse(xbox6.Text), int.Parse(ybox6.Text)] = true;
            }

            if (inbox7.Checked)
            {
                number_of_points++;
                inpoints[6, 0] = int.Parse(xbox7.Text);
                inpoints[6, 1] = int.Parse(ybox7.Text);
                inputpointsarray[int.Parse(xbox7.Text), int.Parse(ybox7.Text)] = true;
            }

            if (inbox8.Checked)
            {
                number_of_points++;
                inpoints[7, 0] = int.Parse(xbox8.Text);
                inpoints[7, 1] = int.Parse(ybox8.Text);
                inputpointsarray[int.Parse(xbox8.Text), int.Parse(ybox8.Text)] = true;
            }

            if (inboxExtra.Checked) // MDW_V2 extra sources from file
            {
                //int npts = inpoints.GetLength(0);
                int npts = number_of_points;

                string FILE_NAME = this.infileExtra.Text;
                if (!File.Exists(FILE_NAME))
                {
                    MessageBox.Show("Unable to find the extra input source file, " + FILE_NAME + ". Extra sources disabled: model will continue with other data loaded.");
                    this.inboxExtra.Checked = false;
                }
                else
                {
                    try
                    {
                        StreamReader sr = File.OpenText(FILE_NAME);

                        while ((input = sr.ReadLine()) != null)
                        {
                            inc++;
                            if (input.Length == 0) { continue; } // skip empty lines
                            if (input[0].CompareTo(commentline) == 0) { continue; } // skip commented lines

                            string[] lineArray;
                            lineArray = input.Split(delimiterChars, StringSplitOptions.RemoveEmptyEntries);

                            if (lineArray.Length != 3)
                            {
                                MessageBox.Show("Problem with line " + inc + " of " + FILE_NAME + ": " + input + "\n\nThe data format should have 3 columns (X, Y, filename). Skipping data.");
                                continue;
                            }

                            // parse data first, in case of exception
                            fExtra = lineArray[2];
                            if (!File.Exists(fExtra)) // check that the file exists
                            {
                                MessageBox.Show("Problem with line " + inc + " of " + FILE_NAME + ": file " + fExtra + " does not exist. Skipping data.");
                                continue;
                            }
                            xExtra = int.Parse(lineArray[0]);
                            yExtra = int.Parse(lineArray[1]);
                            nExtra++;

                            // add points to expanded inpoints array
                            if (inpoints.GetLength(0) < (npts + nExtra))
                            {
                                inpoints = Resize2DintArray(inpoints, new int[] { npts + nExtra, 2 });
                            }
                            inpoints[npts + nExtra - 1, 0] = xExtra;
                            inpoints[npts + nExtra - 1, 1] = yExtra;
                            inputpointsarray[xExtra, yExtra] = true;

                            // store file name in string array
                            Array.Resize(ref extraSourceFiles, nExtra);
                            extraSourceFiles[nExtra - 1] = fExtra;

                            //MessageBox.Show("Extra source: " + inpoints[npts + nExtra - 1, 0] + ", " + inpoints[npts + nExtra - 1, 1] + ", " + extraSourceFiles[nExtra - 1]);
                            number_of_points++;

                        }
                        sr.Close();
                        if (nExtra == 0)
                        {
                            MessageBox.Show("No valid extra sources found in file " + FILE_NAME + ". Extra sources disabled: model will continue with other data loaded.");
                            this.inboxExtra.Checked = false;
                        }
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("There was some type of error loading the extra source data from " + FILE_NAME + " at line " + Convert.ToString(inc) + ". CAESAR will continue to function but may not be correct" +
                                        "\n\nDebug info: \n" + e.Message + "\n\nStackTrace:\n" + e.StackTrace); //MDW_Apr24: added exception message"
                    }

                }
            }

            inputfile = new double[number_of_points, (int)((maxcycle * 60) / input_time_step) + 10, 14]; // MDW_V2 correction to size of grid: file width of 14, not 16

            stage_inputfile = new double[(int)((maxcycle * 60) / stage_input_time_step) + 10];

            elev = new double[xmax + 2, ymax + 2];
            water_depth = new double[xmax + 2, ymax + 2];
            angle_threshold = new int[xmax + 2, ymax + 2];

            old_j_mean_store = new double[(int)((maxcycle * 60) / input_time_step) + 10];

            qx = new double[xmax + 2, ymax + 2];
            qy = new double[xmax + 2, ymax + 2];

            qx_prev = new double[xmax + 2, ymax + 2]; // OIL_V1 for saving of previous water routing
            qy_prev = new double[xmax + 2, ymax + 2];

            qxs = new double[xmax + 2, ymax + 2, tracers + 1];
            qys = new double[xmax + 2, ymax + 2, tracers + 1];


            // temp arrays that control how much sediment goes up,. righ left down and into suspended sediment.
            sr = new Double[xmax + 2, ymax + 2, 10, tracers + 1];
            sl = new Double[xmax + 2, ymax + 2, 10, tracers + 1];
            su = new Double[xmax + 2, ymax + 2, 10, tracers + 1];
            sd = new Double[xmax + 2, ymax + 2, 10, tracers + 1];
            ss = new Double[xmax + 2, ymax + 2, tracers + 1];

            erodetot = new Double[xmax + 2, ymax + 2];
            erodetot3 = new Double[xmax + 2, ymax + 2];
            temp_elev = new Double[xmax + 2, ymax + 2];


            Vel = new double[xmax + 2, ymax + 2];
            area = new double[xmax + 2, ymax + 2];
            index = new int[xmax + 2, ymax + 2];
            elev_diff = new double[xmax + 2, ymax + 2];

            bedrock = new double[xmax + 2, ymax + 2];
            tempcreep = new double[xmax + 2, ymax + 2];
            init_elevs = new double[xmax + 2, ymax + 2];
            vel_dir = new double[xmax + 2, ymax + 2, 9];

            strata = new double[((xmax + 2) * (ymax + 2)) / LIMIT, 10, G_MAX + 1, tracers + 1];
            grain = new double[((2 + xmax) * (ymax + 2)) / LIMIT, G_MAX + 1, tracers + 1];


            cross_scan = new int[xmax + 2, ymax + 2];
            down_scan = new int[ymax + 2, xmax + 2];
            // time step for rain data
            rain_data_time_step = double.Parse(raintimestepbox.Text);
            mfiletimestep = double.Parse(mfiletimestepbox.Text);

            // line to stop max time step being greater than rain time step
            if (rain_data_time_step < 1) rain_data_time_step = 1;
            if (max_time_step / 60 > rain_data_time_step) max_time_step = (int)rain_data_time_step * 60;

            hourly_rain_data = new double[(int)(maxcycle * (60 / rain_data_time_step)) + 100, rfnum + 1];
            hourly_m_value = new double[(int)(maxcycle * (60 / mfiletimestep)) + 10, rfnum + 1];
            climate_data = new double[10001, 3];

            // TEMP_V1 - meteorological forcing array allocation
            int met_array_length = (int)(maxcycle * (60 / met_data_time_step)) + 100;
            hourly_air_temp = new double[met_array_length, nMetZones];
            hourly_shortwave = new double[met_array_length, nMetZones];
            hourly_windspeed = new double[met_array_length, nMetZones];
            hourly_humidity = new double[met_array_length, nMetZones];
            hourly_cloudcover = new double[met_array_length, nMetZones];
            hourly_pressure = new double[met_array_length, nMetZones];
            hourly_dewpoint = new double[met_array_length, nMetZones];

            temp_grain = new double[G_MAX + 1];
            veg = new Double[xmax + 1, ymax + 1, 4]; // 0 is elevation, 1 is level of veg cover (ranging from 0 to 1)
            edge = new double[xmax + 1, ymax + 1]; // TJC 27/1/05
            edge2 = new double[xmax + 1, ymax + 1]; // TJC 27/4/06

            Tau = new double[xmax + 2, ymax + 2];

            catchment_input_x_coord = new int[xmax * ymax];
            catchment_input_y_coord = new int[xmax * ymax];

            //dune things
            dune_mult = (int)(DX) / int.Parse(dune_grid_size_box.Text);
            if (dune_mult < 1) dune_mult = 1;
            if (DuneBox.Checked == false) dune_mult = 1; // needed in order to stop it tripping out the memory

            area_depth = new double[xmax + 2, ymax + 2];
            sand = new double[xmax + 2, ymax + 2];
            elev2 = new double[(xmax * dune_mult) + 2, (ymax * dune_mult) + 2];
            sand2 = new double[(xmax * dune_mult) + 2, (ymax * dune_mult) + 2];

            // MJ arrays 08/02/05

            Vsusptot = new double[xmax + 2, ymax + 2, tracers + 1];

            // JOE arrays
            slopeAnalysis = new double[xmax + 2, ymax + 2];     // <JOE 20051605>
            aspect = new double[xmax + 2, ymax + 2];                // <JOE 20051605>
            hillshade = new double[xmax + 2, ymax + 2];         // <JOE 20051605>

            //Gez arrays
            sum_grain = new double[G_MAX + 1, tracers + 1];
            sum_grain2 = new double[G_MAX + 1, tracers + 1];
            old_sum_grain = new double[G_MAX + 1, tracers + 1];
            old_sum_grain2 = new double[G_MAX + 1, tracers + 1];
            Qg_step = new double[G_MAX + 1, tracers + 1];
            Qg_hour = new double[G_MAX + 1, tracers + 1];
            Qg_over = new double[G_MAX + 1, tracers + 1];
            Qg_last = new double[G_MAX + 1, tracers + 1];
            Qg_step2 = new double[G_MAX + 1, tracers + 1];
            Qg_hour2 = new double[G_MAX + 1, tracers + 1];
            Qg_over2 = new double[G_MAX + 1, tracers + 1];
            Qg_last2 = new double[G_MAX + 1, tracers + 1];

            CATCH_FILE = TimeseriesOutBox.Text; // MJ 18/01/05
            //CATCH_FILE2 = tracerOutputtextBox.Text;

            // siberia data
            Beta1 = double.Parse(Beta1Box.Text);
            Beta3 = double.Parse(Beta3Box.Text);
            m1 = double.Parse(m1Box.Text);
            m3 = double.Parse(m3Box.Text);
            n1 = double.Parse(n1Box.Text);

            // sediment tpt law
            if (einsteinbox.Checked == true) einstein = 1;
            if (wilcockbox.Checked == true) wilcock = 1;
            if (meyerbox.Checked == true) meyer = 1;

            div_inputs = int.Parse(div_inputs_box.Text);
            recirculate_proportion = double.Parse(propremaining.Text);

            graphics_scale = (int)(1500 / xmax); // auto scales the graphics to be roughly 1000 pixels wide.
            // Create a drawing surface with the same dimensions DEM * graphics scale
            if (graphics_scale < 1) graphics_scale = 1; // catch to prevent too long DEMS causign error..
            m_objDrawingSurface = new Bitmap(xmax * graphics_scale,
               ymax * graphics_scale, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            // tidal/stagevariables
            fromx = int.Parse(TidalXmin.Text);
            tox = int.Parse(TidalXmax.Text);
            fromy = int.Parse(TidalYmin.Text);
            toy = int.Parse(TidalYmax.Text);

            // soil generation variables
            P1 = double.Parse(textBox11.Text);
            b1 = double.Parse(textBox12.Text);
            k1 = double.Parse(textBox13.Text);
            c1 = double.Parse(textBox14.Text);
            c2 = double.Parse(textBox15.Text);
            k2 = double.Parse(textBox16.Text);
            c3 = double.Parse(textBox17.Text);
            c4 = double.Parse(textBox18.Text);

            // disributed hydro model arrays
            j = new Double[rfnum + 1]; //0.000000001; //start of hydrological model paramneters
            jo = new Double[rfnum + 1];//0.000000001;
            j_mean = new Double[rfnum + 1];
            old_j_mean = new Double[rfnum + 1];
            new_j_mean = new Double[rfnum + 1];
            rfarea = new int[xmax + 2, ymax + 2];
            nActualGridCells = new int[rfnum + 1];
            catchment_input_counter = new int[rfnum + 1];
            M = new Double[rfnum + 1];

            // spat var mannings
            spat_var_mannings = new Double[xmax + 1, ymax + 1];

            // tracer stuff
            tracer_area = new int[xmax + 2, ymax + 2];

            // grain stuff
            grain_area = new int[xmax + 2, ymax + 2];

            ///////////////////////////////////////////////////////////////////////////
            // Additional variables inserted by MDW

            // Solute tracer - MDW_V2 [needs to be done after load_data
            //if (isTraceSolutes == true)
            //{
            //    //nSolutes = int.Parse(numBoxSoluteNumber.Text);
            //
            //}

            // if is
            // rainfall zonation map - for water source tracing. MDW 01/04/16
            rainzonation = new int[xmax + 2, ymax + 2];
            for (int x = 1; x <= xmax; x++)
            {
                for (int y = 1; y <= ymax; y++)
                {
                    rainzonation[x, y] = 0;
                }
            }

            // TEMP_V1 - water temperature and met-zonation array allocation
            water_temp = new double[xmax + 2, ymax + 2];
            water_temp_prev = new double[xmax + 2, ymax + 2];
            met_zonation = new int[xmax + 2, ymax + 2];
            for (int x = 1; x <= xmax; x++)
            {
                for (int y = 1; y <= ymax; y++)
                {
                    met_zonation[x, y] = 0;
                }
            }

            // mine input stuff tc 5/23
            mine_inputs = new double[500, 5]; // number, (x, y, vol, tracer fraction, GS fraction)





        }

        private static double[,,] Resize3DdoubleArray(double[,,] arr, int[] newSizes)
        {
            // MDW_V2: resizes a 3D double array. Allows unknown numbers of sources etc.
            // Best to limit use to startup only (memory/ computationally expensive).
            // New values will be zero.
            // taken from here: https://stackoverflow.com/questions/11348493/resizing-a-3d-array
            if (newSizes.Length != arr.Rank)
                throw new ArgumentException("arr must have the same number of dimensions " +
                                            "as there are elements in newSizes", "newSizes");
            var newArray = new double[newSizes[0], newSizes[1], newSizes[2]];
            var xMin = Math.Min(newSizes[0], arr.GetLength(0));
            var yMin = Math.Min(newSizes[1], arr.GetLength(1));
            var zMin = Math.Min(newSizes[2], arr.GetLength(2));
            for (var x = 0; x < xMin; x++)
                for (var y = 0; y < yMin; y++)
                    for (var z = 0; z < zMin; z++)
                        newArray[x, y, z] = arr[x, y, z];
            return newArray;
        }
        private static int[,] Resize2DintArray(int[,] arr, int[] newSizes)
        {
            // MDW_V2: resizes a 2D integer array. Allows unknown numbers of sources etc.
            // Best to limit use to startup only (memory/ computationally expensive).
            // New values will be zero.
            // taken from here: https://stackoverflow.com/questions/11348493/resizing-a-3d-array

            if (newSizes.Length != arr.Rank)
                throw new ArgumentException("arr must have the same number of dimensions " +
                                            "as there are elements in newSizes", "newSizes");
            var newArray = new int[newSizes[0], newSizes[1]];
            var xMin = Math.Min(newSizes[0], arr.GetLength(0));
            var yMin = Math.Min(newSizes[1], arr.GetLength(1));
            for (var x = 0; x < xMin; x++)
                for (var y = 0; y < yMin; y++)
                    newArray[x, y] = arr[x, y];
            return newArray;
        }


        /*
                    if (newSizes.Length != arr.Rank)
                        throw new ArgumentException("arr must have the same number of dimensions " +
                                                    "as there are elements in newSizes", "newSizes");

                    var temp = Array.CreateInstance(arr.GetType().GetElementType(), newSizes);

                    var temp = new T[xSize, ySize, zSize];

                    int length = arr.Length <= temp.Length ? arr.Length : temp.Length;

                    int xMin = Math.Min(newSizes[0], arr.GetLength(0));
                    int yMin = Math.Min(newSizes[1], arr.GetLength(1));
                    int zMin = 0;
                    if (newSizes.Length == 3)
                    {
                        zMin = Math.Min(newSizes[2], arr.GetLength(2));
                    }
                    //Array.ConstrainedCopy(arr, 0, temp, 0, length);

                    for (int x = 0; x < xMin; x++)
                    {
                        for (int y = 0; y < yMin; y++)
                        {
                            if (arr.Rank == 3)
                            {
                                for (int z = 0; z < zMin; z++)
                                {
                                    temp[x, y, z] = arr[x, y, z];
                                }
                            }
                            else
                            {

                            }
                        }
                    }

                    return temp;


                    T[,,] ResizeArray<T>(T[,,] original, int xSize, int ySize, int zSize)
                    {
                        var newArray = new T[xSize, ySize, zSize];
                        var xMin = Math.Min(xSize, original.GetLength(0));
                        var yMin = Math.Min(ySize, original.GetLength(1));
                        var zMin = Math.Min(zSize, original.GetLength(2));

                        return newArray;
                    }



                }
        */
        void sort_active(int x, int y)
        {
            int xyindex;
            double total;
            double amount;
            double coeff;
            int n, z;
            int G = grain_area[x, y];


            if (index[x, y] == -9999) addGS(x, y);  // should not be necessary
            xyindex = index[x, y];

            total = 0.0;
            for (n = 0; n <= G_MAX; n++)
            {

                for (int T = 0; T <= tracers; T++) total += grain[xyindex, n, T];

            }

            if (total > (active * 1.5)) // depositing - create new strata layer and remove bottom one..
            {
                // start from bottom
                // remove bottom active layer
                // then move all from layer above into one below, up to the top layer
                for (z = 9; z >= 1; z--)
                {
                    for (n = 0; n <= G_MAX - 2; n++)
                    {

                        for (int T = 0; T <= tracers; T++) strata[xyindex, z, n, T] = strata[xyindex, z - 1, n, T];

                    }
                }

                // then remove strata thickness from grain - and add to top strata layer
                coeff = active / total;
                for (n = 1; n <= (G_MAX - 1); n++)
                {
                    for (int T = 0; T <= tracers; T++)
                    {
                        if ((grain[xyindex, n, T] > 0.0))
                        {

                            amount = coeff * (grain[xyindex, n, T]);
                            strata[xyindex, 0, n - 1, T] = amount;
                            grain[xyindex, n, T] -= amount;

                        }
                    }
                }
            }

            if (total < (active / 4)) // eroding - eat into existing strata layer & create new one at bottom
            {
                // Start at top
                // Add top strata to grain
                for (n = 1; n <= (G_MAX - 1); n++)
                {
                    for (int T = 0; T <= tracers; T++) grain[xyindex, n, T] += strata[xyindex, 0, n - 1, T];
                }

                // then from top down add lower strata into upper
                for (z = 0; z <= 8; z++)
                {
                    for (n = 0; n <= G_MAX - 2; n++)
                    {
                        for (int T = 0; T <= tracers; T++) strata[xyindex, z, n, T] = strata[xyindex, z + 1, n, T];
                    }
                }

                // add new layer at the bottom
                amount = active;
                z = 9;
                for (n = 1; n <= G_MAX - 1; n++)
                {
                    int TT = tracer_area[x, y];// TT= tracer area....

                    // particle size distribution 1
                    if (G == 0)
                    { strata[xyindex, z, n - 1, TT] = amount * dprop[n]; } // 0.0;
                    // particle size distribution 2
                    else if (G == 1)
                    { strata[xyindex, z, n - 1, TT] = amount * dprop_[n]; }
                }
            }

        }


        //double layer_depth(int index1, int t)
        //{

        //    double total=0;

        //    total+=(grain[index1,t,0]);

        //    return (total);
        //}

        //double active_layer_depth(int index1)
        //{
        //    int n;
        //    double active_thickness = 0;

        //    for (n = 1; n <= G_MAX; n++)
        //    {
        //       active_thickness += (grain[index1, n, 0]);
        //    }
        //    return(active_thickness);
        //}


        double sand_fraction(int index1)
        {

            int n;
            double active_thickness = 0;
            double sand_total = 0;
            for (n = 1; n <= G_MAX; n++)
            {
                for (int T = 0; T <= tracers; T++) active_thickness += (grain[index1, n, T]);
            }

            for (n = 1; n <= 2; n++) // number of sand fractions...
            {
                for (int T = 0; T <= tracers; T++) sand_total += (grain[index1, n, T]);
            }

            if (active_thickness < 0.0001)
            {
                return (0.0);
            }
            else
            {
                return (sand_total / active_thickness);
            }

        }


        double d50(int index1)
        {
            int z, n, i;
            double active_thickness = 0;
            double Dfifty = 0, max = 0, min = 0;
            double[] cum_tot;
            cum_tot = new double[20];

            for (n = 1; n <= G_MAX; n++)
            {
                for (z = 0; z <= (0); z++)
                {
                    for (int T = 0; T <= tracers; T++) active_thickness += (grain[index1, n, T]);
                    cum_tot[n] += active_thickness;
                }
            }


            i = 1;
            while (cum_tot[i] < (active_thickness * 0.5) && i <= G_MAX - 1)
            {
                i++;
            }

            if (i == 1) { min = Math.Log(d1); max = Math.Log(d1); }
            if (i == 2) { min = Math.Log(d1); max = Math.Log(d2); }
            if (i == 3) { min = Math.Log(d2); max = Math.Log(d3); }
            if (i == 4) { min = Math.Log(d3); max = Math.Log(d4); }
            if (i == 5) { min = Math.Log(d4); max = Math.Log(d5); }
            if (i == 6) { min = Math.Log(d5); max = Math.Log(d6); }
            if (i == 7) { min = Math.Log(d6); max = Math.Log(d7); }
            if (i == 8) { min = Math.Log(d7); max = Math.Log(d8); }
            if (i == 9) { min = Math.Log(d8); max = Math.Log(d9); }
            //if(i==9){min=Math.Log(d8);max=Math.Log(d9);}

            Dfifty = Math.Exp(max - ((max - min) * ((cum_tot[i] - (active_thickness * 0.5)) / (cum_tot[i] - cum_tot[i - 1]))));
            if (active_thickness < 0.0000001) Dfifty = 0;
            return Dfifty;

        }


        double max_bed_slope2(int x, int y)
        {

            double slope = 0;
            int slopetot = 0;
            double slopemax = 0;

            if (elev[x, y] > elev[x, y - 1])
            {
                slope = Math.Pow((elev[x, y] - elev[x, y - 1]) / DX, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x + 1, y - 1])
            {
                slope = Math.Pow((elev[x, y] - elev[x + 1, y - 1]) / root, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x + 1, y])
            {
                slope = Math.Pow((elev[x, y] - elev[x + 1, y]) / DX, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x + 1, y + 1])
            {
                slope = Math.Pow((elev[x, y] - elev[x + 1, y + 1]) / root, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x, y + 1])
            {
                slope = Math.Pow((elev[x, y] - elev[x, y + 1]) / DX, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x - 1, y + 1])
            {
                slope = Math.Pow((elev[x, y] - elev[x - 1, y + 1]) / root, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x - 1, y])
            {
                slope = Math.Pow((elev[x, y] - elev[x - 1, y]) / DX, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            if (elev[x, y] > elev[x - 1, y - 1])
            {
                slope = Math.Pow((elev[x, y] - elev[x - 1, y - 1]) / root, 1);
                if (slope > slopemax) slopemax = slope;
                slopetot++;
            }
            //if (slope > 0) slope = (slope / slopetot);


            //return(slopemax);
            //if(slopemax<0.001)slopemax=0.001;
            //if (slopemax > 0.01) slopemax = 0.01;
            return (slopemax);
        }



        void addGS(int x, int y)
        {
            // needs lock statement to stop two being added at the same time...
            lock (this)
            {

                int n, q;
                grain_array_tot++;
                index[x, y] = grain_array_tot;
                int T = tracer_area[x, y];
                int G = grain_area[x, y];

                grain[grain_array_tot, 0, T] = 0;
                for (n = 1; n <= G_MAX - 1; n++)
                {
                    // particle size distribution 1
                    if (G == 0)
                    { grain[grain_array_tot, n, T] = active * dprop[n]; }

                    // particle size distribution 2
                    else if (G == 1)
                    { grain[grain_array_tot, n, T] = active * dprop_[n]; }

                }
                grain[grain_array_tot, G_MAX, T] = 0;


                for (n = 0; n <= 9; n++)
                {
                    for (int n2 = 0; n2 <= G_MAX - 2; n2++)
                    {
                        // particle size distribution 1
                        if (G == 0)
                        { strata[grain_array_tot, n, n2, T] = (active) * dprop[n2 + 1]; }

                        // particle size distribution 2
                        else if (G == 1)
                        { strata[grain_array_tot, n, n2, T] = (active) * dprop_[n2 + 1]; }
                    }


                    if (elev[x, y] - (active * (n + 1)) < (bedrock[x, y] - active))
                    {
                        for (q = 0; q <= (G_MAX - 2); q++)
                        {
                            strata[grain_array_tot, n, q, T] = 0;
                        }
                    }
                }


                sort_active(x, y);

            }

        }

        // calc roughtness on Srickler relationship
        double strickler(int x, int y)
        {
            double temp = 0;
            double temp_d50 = 1;

            if (index[x, y] != -9999) temp_d50 = d50(index[x, y]);
            temp = 0.034 * Math.Pow(temp_d50, 0.167);
            //Console.WriteLine(Convert.ToString(strickler(x,y)));
            temp += 0.012;
            return temp;

        }


        ////Calculate roughness - Added JMW 20051122
        ////Based on Baptist, 2005
        //double chezy(int x, int y)
        //{
        //    double Cr;
        //    double Cb;
        //    double temp_d50=30;

        //    double temp_depth=water_depth[x,y];

        //    if(temp_depth<=0)temp_depth=0.1;


        //    // just to save it when there is no grain file
        //    if (index[x,y] != -9999)temp_d50=d50(index[x,y]);



        //    // Grain roughness (strickler formulation)
        //    Cb = (26.4*Math.Pow(temp_depth/temp_d50,0.167));
        //    Cr=Cb;

        //    // veg influencing roughness is commented out at the moment...
        //    //if (veg[x,y,1]>0.01)// if there is any veg..
        //    //{
        //    //    // Applying vegetation
        //    //    if (veg[x,y,3] < temp_depth)
        //    //    {
        //    //        Cr = (Math.Pow(1/(1/(Cb*Cb) + veg[x,y,2]*veg[x,y,3]/(2*g)),0.5)
        //    //            + Math.Pow(g,0.5)/kappa*Math.Log(temp_depth/veg[x,y,3]));
        //    //    }
        //    //    else
        //    //    {
        //    //        Cr = (Math.Pow(1/(1/(Cb*Cb) + veg[x,y,2]*temp_depth/(2*g)),0.5));
        //    //    }
        //    //}

        //    return Cr;
        //}


        //double chezytomanning(double chezyc, double depth)
        //{
        //    return (Math.Pow(depth,0.167))/chezyc;
        //}


        //double calc_manning(int x, int y)
        //{
        //    double temp_depth = water_depth[x, y];
        //    if (temp_depth <= 0) temp_depth = 0.1;
        //    return chezytomanning(chezy(x, y), temp_depth) + 0.012;
        //    return 0.03 + (veg[x, y, 2] * 0.03);
        //    if (veg[x, y, 1] > 0.5)
        //    {
        //        return 0.05;
        //    }
        //    else
        //    {
        //        return 0.03;
        //    }
        //}


        void grow_grass(double amount3)
        {

            int x, y;
            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    //first check if veg is at 0.. not sure if this is needed now..
                    if (veg[x, y, 0] == 0)
                    {
                        veg[x, y, 0] = elev[x, y];
                    }

                    // first check if under water or not..
                    if (water_depth[x, y] < water_depth_erosion_threshold)
                    {
                        // if not then it
                        // now adds to the amount of veg there..
                        veg[x, y, 1] += amount3;
                        if (veg[x, y, 1] > 1) veg[x, y, 1] = 1;
                    }

                    // check if veg below elev! if so raise to elev
                    if (veg[x, y, 0] < elev[x, y]) // raises the veg level if it gets buried.. but only by a certain amount (0.001m day...)
                    {
                        veg[x, y, 0] += 0.001; // this is an arbitrary amount..
                        if (veg[x, y, 0] > elev[x, y]) veg[x, y, 0] = elev[x, y];
                    }

                    // check if veg above elev! if so lower to elev
                    if (veg[x, y, 0] > elev[x, y])
                    {
                        veg[x, y, 0] -= 0.001; // this is an arbitrary amount..
                        if (veg[x, y, 0] < elev[x, y]) veg[x, y, 0] = elev[x, y];
                    }

                    // trying to see now, if veg is above elev - so if lateral erosion happens - more than 0.1m the veg gets wiped..

                    if (veg[x, y, 0] - elev[x, y] > 0.1)
                    {
                        veg[x, y, 1] = 0;
                        veg[x, y, 0] = elev[x, y];
                    }


                    // now see if veg under water - if so let it die back a bit..
                    if (water_depth[x, y] > water_depth_erosion_threshold && veg[x, y, 1] > 0)
                    {
                        veg[x, y, 1] -= (amount3 / 2);
                        if (veg[x, y, 1] < 0)
                        {
                            veg[x, y, 1] = 0;
                            veg[x, y, 0] = elev[x, y]; // resets elev if veg amt is 0
                        }
                    }

                    // also if it is under sediment - then dies back a bit too...
                    if (veg[x, y, 0] < elev[x, y])
                    {
                        veg[x, y, 1] -= (amount3 / 2);
                        if (veg[x, y, 1] < 0)
                        {
                            veg[x, y, 1] = 0;
                            veg[x, y, 0] = elev[x, y]; // resets elev if veg amt is 0
                        }

                    }

                    // but if it is under sediment, has died back to nearly 0 (0.05) then it resets the elevation
                    // to the surface elev.
                    if (veg[x, y, 0] < elev[x, y] && veg[x, y, 1] < 0.05)
                    {
                        veg[x, y, 0] = elev[x, y];
                    }


                }
            }


        }


        void creep(double time)
        {

            /** creep rate is 10*-2 * slope per year, so inputs time jump in years*/
            /** very important differnece here is that slide_GS() is called only if
				BOTH cells are not -9999 therfore if both have grainsize then do additions.
				this is to stop the progressive spread of selected cells upslope */



            int x, y;
            double temp;


            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    tempcreep[x, y] = 0;
                }
            }


            for (x = 2; x < xmax; x++)
            {
                for (y = 2; y < ymax; y++)
                {
                    if (elev[x, y] > bedrock[x, y])
                    {
                        if (elev[x, y - 1] < elev[x, y] && elev[x, y - 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x, y - 1]) / DX) * CREEP_RATE * time / DX;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x, y - 1] += temp;
                            if (index[x, y - 1] != -9999)
                            {
                                slide_GS(x, y, temp, x, y - 1);
                            }
                        }
                        if (elev[x + 1, y - 1] < elev[x, y] && elev[x + 1, y - 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x + 1, y - 1]) / root) * CREEP_RATE * time / root;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y - 1] += temp;
                            if (index[x + 1, y - 1] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y - 1);
                            }

                        }
                        if (elev[x + 1, y] < elev[x, y] && elev[x + 1, y] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x + 1, y]) / DX) * CREEP_RATE * time / DX;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y] += temp;
                            if (index[x + 1, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y);
                            }
                        }
                        if (elev[x + 1, y + 1] < elev[x, y] && elev[x + 1, y + 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x + 1, y + 1]) / root) * CREEP_RATE * time / root;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y + 1] += temp;
                            if (index[x + 1, y + 1] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y + 1);
                            }
                        }
                        if (elev[x, y + 1] < elev[x, y] && elev[x, y + 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x, y + 1]) / DX) * CREEP_RATE * time / DX;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x, y + 1] += temp;
                            if (index[x, y + 1] != -9999)
                            {
                                slide_GS(x, y, temp, x, y + 1);
                            }
                        }
                        if (elev[x - 1, y + 1] < elev[x, y] && elev[x - 1, y + 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x - 1, y + 1]) / root) * CREEP_RATE * time / root;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y + 1] += temp;
                            if (index[x - 1, y + 1] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y + 1);
                            }
                        }
                        if (elev[x - 1, y] < elev[x, y] && elev[x - 1, y] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x - 1, y]) / DX) * CREEP_RATE * time / DX;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y] += temp;
                            if (index[x - 1, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y);
                            }
                        }
                        if (elev[x - 1, y - 1] < elev[x, y] && elev[x - 1, y - 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x - 1, y - 1]) / root) * CREEP_RATE * time / root;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y - 1] += temp;
                            if (index[x - 1, y - 1] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y - 1);
                            }
                        }
                    }
                }
            }

            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    elev[x, y] += tempcreep[x, y];
                }
            }

        }

        void siberia(double time)
        {
            int x, y;
            double temp;
            double SibQ = 0;
            double SibQsf = 0;



            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    tempcreep[x, y] = 0;
                }
            }


            for (x = 2; x < xmax; x++)
            {
                for (y = 2; y < ymax; y++)
                {
                    if (elev[x, y] > bedrock[x, y])
                    {
                        //SibQ =  Math.Pow(area[x, y] * DX * DX, m3);
                        //SibQsf = DX * Beta3 * Beta1 * Math.Pow((SibQ), m1) * Math.Pow(max_bed_slope2(x, y), n1) * time;

                        SibQ = Beta3 * Math.Pow(area[x, y] * DX * DX, m3);
                        SibQsf = Beta1 * Math.Pow((SibQ / DX), m1) * Math.Pow(max_bed_slope2(x, y), n1) * time;
                        double max_slope = max_bed_slope2(x, y);
                        if (max_slope == 0) max_slope = 10; // catch line to stop erosion if there is no slope
                        //if (max_slope > 0) max_slope *= 0.99;

                        if ((elev[x, y] - elev[x, y - 1]) / DX >= max_slope && elev[x, y - 1] > -9999)
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x, y - 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x, y - 1);
                            }
                        }
                        if ((elev[x, y] - elev[x + 1, y - 1]) / root >= max_slope && (elev[x + 1, y - 1] > -9999 || x == xmax))
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y - 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y - 1);
                            }

                        }
                        if ((elev[x, y] - elev[x + 1, y]) / DX >= max_slope && (elev[x + 1, y] > -9999 || x == xmax))
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y);
                            }
                        }
                        if ((elev[x, y] - elev[x + 1, y + 1]) / root >= max_slope && (elev[x + 1, y + 1] > -9999 || x == xmax))
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y + 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y + 1);
                            }
                        }
                        if ((elev[x, y] - elev[x, y + 1]) / DX >= max_slope && elev[x, y + 1] > -9999)
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x, y + 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x, y + 1);
                            }
                        }
                        if ((elev[x, y] - elev[x - 1, y + 1]) / root >= max_slope && elev[x - 1, y + 1] > -9999)
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y + 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y + 1);
                            }
                        }
                        if ((elev[x, y] - elev[x - 1, y]) / DX >= max_slope && elev[x - 1, y] > -9999)
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y);
                            }
                        }
                        if ((elev[x, y] - elev[x - 1, y - 1]) / root >= max_slope && elev[x - 1, y - 1] > -9999)
                        {
                            temp = SibQsf;
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y - 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y - 1);
                            }
                        }

                    }
                }
            }

            for (x = 2; x < xmax; x++)
            {
                for (y = 2; y < ymax; y++)
                {
                    elev[x, y] += tempcreep[x, y];
                }
            }

        }


        void soilerosion(double time)
        {

            /** creep rate is 10*-2 * slope per year, so inputs time jump in years*/
            /** very important differnece here is that slide_GS() is called only if
                BOTH cells are not -9999 therfore if both have grainsize then do additions.
                this is to stop the progressive spread of selected cells upslope */



            int x, y;
            double temp;


            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    tempcreep[x, y] = 0;
                }
            }


            for (x = 2; x < xmax; x++)
            {
                for (y = 2; y < ymax; y++)
                {
                    if (elev[x, y] > bedrock[x, y])
                    {
                        if (elev[x, y - 1] < elev[x, y] && elev[x, y - 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x, y - 1]) / DX) * SOIL_RATE * time / DX * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x, y - 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x, y - 1);
                            }
                        }
                        if (elev[x + 1, y - 1] < elev[x, y] && elev[x + 1, y - 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x + 1, y - 1]) / root) * SOIL_RATE * time / root * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y - 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y - 1);
                            }

                        }
                        if (elev[x + 1, y] < elev[x, y] && elev[x + 1, y] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x + 1, y]) / DX) * SOIL_RATE * time / DX * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y);
                            }
                        }
                        if (elev[x + 1, y + 1] < elev[x, y] && elev[x + 1, y + 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x + 1, y + 1]) / root) * SOIL_RATE * time / root * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x + 1, y + 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x + 1, y + 1);
                            }
                        }
                        if (elev[x, y + 1] < elev[x, y] && elev[x, y + 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x, y + 1]) / DX) * SOIL_RATE * time / DX * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x, y + 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x, y + 1);
                            }
                        }
                        if (elev[x - 1, y + 1] < elev[x, y] && elev[x - 1, y + 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x - 1, y + 1]) / root) * SOIL_RATE * time / root * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y + 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y + 1);
                            }
                        }
                        if (elev[x - 1, y] < elev[x, y] && elev[x - 1, y] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x - 1, y]) / DX) * SOIL_RATE * time / DX * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y);
                            }
                        }
                        if (elev[x - 1, y - 1] < elev[x, y] && elev[x - 1, y - 1] > -9999)
                        {
                            temp = ((elev[x, y] - elev[x - 1, y - 1]) / root) * SOIL_RATE * time / root * Math.Pow(area[x, y] * DX * DX, 0.5);
                            if ((elev[x, y] - temp) < bedrock[x, y]) temp = elev[x, y] - bedrock[x, y];
                            if (temp < 0) temp = 0;
                            tempcreep[x, y] -= temp;
                            tempcreep[x - 1, y - 1] += temp;
                            if (index[x, y] != -9999)
                            {
                                slide_GS(x, y, temp, x - 1, y - 1);
                            }
                        }
                    }
                }
            }

            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    elev[x, y] += tempcreep[x, y];
                }
            }

        }


        void get_area()
        {
            int x, y;

            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    area_depth[x, y] = 1;
                    area[x, y] = 0;
                    if (elev[x, y] == -9999)
                    {
                        area_depth[x, y] = 0.0;
                    }

                }
            }
            get_area4();

        }


        void get_area4()
        {

            // new routine for determining drainage area 4/10/2010
            // instead of using sweeps this sorts all the elevations then works frmo the
            // highest to lowest - calculating drainage area - D-infinity basically.

            int x, y, n, x2, y2, dir;

            // zero load of arrays
            double[] tempvalues, tempvalues2, xkey, ykey;
            tempvalues = new Double[(xmax + 2) * (ymax + 2)];
            tempvalues2 = new Double[(xmax + 2) * (ymax + 2)];
            xkey = new Double[(xmax + 2) * (ymax + 2)];
            ykey = new Double[(xmax + 2) * (ymax + 2)];

            // then createst temp array based on elevs then also one for x values.
            int inc = 1;
            for (y = 1; y <= ymax; y++)
            {
                for (x = 1; x <= xmax; x++)
                {
                    tempvalues[inc] = elev[x, y];
                    xkey[inc] = x;
                    inc++;
                }
            }
            // then sorts according to elevations - but also sorts the key (xkey) according to these too..
            Array.Sort(tempvalues, xkey);

            // now does the same for y values
            inc = 1;
            for (y = 1; y <= ymax; y++)
            {
                for (x = 1; x <= xmax; x++)
                {
                    tempvalues2[inc] = elev[x, y];
                    ykey[inc] = y;
                    inc++;
                }
            }

            Array.Sort(tempvalues2, ykey);


            // then works through the list of x and y co-ordinates from highest to lowest...
            for (n = (xmax * ymax); n >= 1; n--)
            {
                x = (int)(xkey[n]);
                //this.InfoStatusPanel.Text = Convert.ToString(x);
                y = (int)(ykey[n]);
                //this.InfoStatusPanel.Text = Convert.ToString(y);

                if (area_depth[x, y] > 0)
                {
                    // update area if area_depth is higher
                    if (area_depth[x, y] > area[x, y]) area[x, y] = area_depth[x, y];

                    double difftot = 0;

                    // work out sum of +ve slopes in all 8 directions
                    for (dir = 1; dir <= 8; dir++)//was 1 to 8 +=2
                    {

                        x2 = x + deltaX[dir];
                        y2 = y + deltaY[dir];
                        if (x2 < 1) x2 = 1; if (y2 < 1) y2 = 1; if (x2 > xmax) x2 = xmax; if (y2 > ymax) y2 = ymax;

                        // swap comment lines below for drainage area from D8 or Dinfinity
                        if (Math.IEEERemainder(dir, 2) != 0)
                        {
                            if (elev[x2, y2] < elev[x, y]) difftot += elev[x, y] - elev[x2, y2];
                        }
                        else
                        {
                            if (elev[x2, y2] < elev[x, y]) difftot += (elev[x, y] - elev[x2, y2]) / 1.414;
                        }
                        //if(elev[x,y]-elev[x2,y2]>difftot)difftot=elev[x,y]-elev[x2,y2];
                    }
                    if (difftot > 0)
                    {
                        // then distribute to all 8...
                        for (dir = 1; dir <= 8; dir++)//was 1 to 8 +=2
                        {

                            x2 = x + deltaX[dir];
                            y2 = y + deltaY[dir];
                            if (x2 < 1) x2 = 1; if (y2 < 1) y2 = 1; if (x2 > xmax) x2 = xmax; if (y2 > ymax) y2 = ymax;

                            // swap comment lines below for drainage area from D8 or Dinfinity

                            if (Math.IEEERemainder(dir, 2) != 0)
                            {
                                if (elev[x2, y2] < elev[x, y]) area_depth[x2, y2] += area_depth[x, y] * ((elev[x, y] - elev[x2, y2]) / difftot);
                            }
                            else
                            {
                                if (elev[x2, y2] < elev[x, y]) area_depth[x2, y2] += area_depth[x, y] * (((elev[x, y] - elev[x2, y2]) / 1.414) / difftot);
                            }

                            //if (elev[x, y] - elev[x2, y2] == difftot) area_depth[x2, y2] += area_depth[x, y];
                        }

                    }
                    // finally zero the area depth...
                    area_depth[x, y] = 0;
                }
            }

        }


        void init_route(int flag, double reach_input_amount, double catchment_input_amount)
        {
            int x, y, inc;

            double w = water_depth_erosion_threshold;


            /*******************************/

            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    cross_scan[x, y] = 0;
                    down_scan[y, x] = 0;
                    //if(water_depth[x,y]==-9999)water_depth[x,y]=0;
                }
            }



            for (x = 1; x <= xmax; x++)
            {
                inc = 1;
                for (y = 1; y <= ymax; y++)
                {
                    if (water_depth[x, y] > 0
                        || water_depth[x - 1, y] > 0
                        || water_depth[x + 1, y] > 0
                        || water_depth[x, y - 1] > 0
                        || water_depth[x, y + 1] > 0)
                    {
                        cross_scan[x, inc] = y;
                        inc++;
                    }
                    //discharge[x,y]=0;
                }
            }

            for (y = 1; y <= ymax; y++)
            {
                inc = 1;
                for (x = xmax; x >= 1; x--)
                {
                    if (water_depth[x, y] > 0
                        || water_depth[x - 1, y] > 0
                        || water_depth[x + 1, y] > 0
                        || water_depth[x, y - 1] > 0
                        || water_depth[x, y + 1] > 0)
                    {
                        down_scan[y, inc] = x;
                        inc++;
                    }
                    //if(input_type_flag==1)discharge[x,y]=0;
                }
            }


            this.InfoStatusPanel.Text = "Running";      // MJ 14/01/05

        }

        //double Fi(int index1, int t)
        //{
        //    int n;
        //    double active_thickness = 0;
        //    double total = 0;

        //    // part to deal with tracers is now hard coded in order to speed things up - with
        //    // an if statement at the start.. saves un-necessary looping


        //    for (n = 1; n <= G_MAX; n++)
        //    {
        //        active_thickness += (grain[index1, n]);
        //    }


        //    total += (grain[index1, t]);

        //    if (active_thickness < 0.0001)
        //    {
        //        return (0.0);
        //    }
        //    else
        //    {
        //        return (total / active_thickness);
        //    }
        //}

        private void calc_J(double cycle)
        {
            for (int n = 1; n <= rfnum; n++)
            {
                double local_rain_fall_rate; /** in m/second **/
                double local_time_step = 60; /** in secs */


                old_j_mean[n] = new_j_mean[n];
                jo[n] = j[n];

                /* Get The M Value From File If One Is Specified */
                if (variable_m_value_flag == 1)
                {
                    M[n] = hourly_m_value[1 + (int)(cycle / mfiletimestep), n];
                }

                local_rain_fall_rate = 0;
                if (hourly_rain_data[(int)(cycle / rain_data_time_step), n] > 0)
                {
                    local_rain_fall_rate = rain_factor * ((hourly_rain_data[(int)(cycle / rain_data_time_step), n] / 1000) / 3600); /** divide by 1000 to make m/hr, then by 3600 for m/sec */
                }

                if (local_rain_fall_rate == 0)
                {
                    j[n] = jo[n] / (1 + ((jo[n] * local_time_step) / M[n]));
                    new_j_mean[n] = M[n] / local_time_step * Math.Log(1 + ((jo[n] * local_time_step) / M[n]));
                }

                if (local_rain_fall_rate > 0)
                {
                    j[n] = local_rain_fall_rate / (((local_rain_fall_rate - jo[n]) / jo[n]) * Math.Exp((0 - local_rain_fall_rate) * local_time_step / M[n]) + 1);
                    new_j_mean[n] = (M[n] / local_time_step) * Math.Log(((local_rain_fall_rate - jo[n]) + jo[n] * Math.Exp((local_rain_fall_rate * local_time_step) / M[n])) / local_rain_fall_rate);
                }
                if (new_j_mean[n] < 0) new_j_mean[n] = 0;
                /**printf("new J mean = %f\n",(new_j_mean*1000));*/
            }

        }

        double calc_max_flow_direction_degrees(int x, int y)
        {
            double deg = 0;
            if (water_depth[x, y] > water_depth_erosion_threshold)
            {
                double xplus = vel_dir[x, y, 3];
                double xminus = vel_dir[x, y, 7];
                double yplus = vel_dir[x, y, 5];
                double yminus = vel_dir[x, y, 1];
                if (xplus < 0) xplus = 0;
                if (xminus < 0) xminus = 0;
                if (yplus < 0) yplus = 0;
                if (yminus < 0) yminus = 0;
                double xbalance = xplus - xminus;
                double ybalance = yminus - yplus;

                //working out max_flow for 12-3pm

                if (xbalance > 0 && ybalance < 0)
                {

                    deg = (Math.Atan(xbalance / Math.Abs(ybalance)) * (180 / 3.142));

                }

                // for 3pm to 6pm
                if (xbalance > 0 && ybalance > 0)
                {
                    deg = 90 + (Math.Atan(ybalance / xbalance) * (180 / 3.142));
                }

                // for 6 - 9
                if (xbalance < 0 && ybalance > 0)
                {
                    deg = 180 + (Math.Atan(Math.Abs(xbalance) / Math.Abs(ybalance)) * (180 / 3.142));
                }

                // for 9-12
                if (xbalance < 0 && ybalance < 0)
                {
                    deg = 270 + (Math.Atan(Math.Abs(ybalance) / Math.Abs(xbalance)) * (180 / 3.142));
                }

                if (xbalance > 0 && ybalance == 0) deg = 90;
                if (xbalance < 0 && ybalance == 0) deg = 270;
                if (ybalance > 0 && xbalance == 0) deg = 180;
                if (ybalance < 0 && xbalance == 0) deg = 0;

                return (deg);

            }
            else
            {
                return -9999.0;
            }


        }




        void qroute()
        {
            double local_time_factor = time_factor;
            if (local_time_factor > (courant_number * (DX / Math.Sqrt(gravity * (maxdepth))))) local_time_factor = courant_number * (DX / Math.Sqrt(gravity * (maxdepth)));

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (elev[x, y] > -9999) // to stop moving water in to -9999's on elev
                    {
                        // add spatial mannings here
                        double temp_mannings = mannings;
                        if (SpatVarManningsCheckbox.Checked == true) temp_mannings = spat_var_mannings[x, y];

                        // routing in x direction
                        if ((water_depth[x, y] > 0 || water_depth[x - 1, y] > 0) && elev[x - 1, y] > -9999)  // need to check water and not -9999 on elev
                        {
                            double hflow = Math.Max(elev[x, y] + water_depth[x, y], elev[x - 1, y] + water_depth[x - 1, y]) -
                                            Math.Max(elev[x - 1, y], elev[x, y]);
                            if (hflow > DX / 2) hflow = DX / 2; // contreversial line...

                            if (hflow > hflow_threshold)
                            {
                                double tempslope = (((elev[x - 1, y] + water_depth[x - 1, y])) -
                                        (elev[x, y] + water_depth[x, y])) / DX;

                                if (x == xmax) tempslope = edgeslope;
                                if (x <= 2) tempslope = 0 - edgeslope;

                                //double oldqx = qx[x, y];
                                qx_prev[x, y] = qx[x, y]; // OIL_V1 save q state
                                qx[x, y] = ((qx[x, y] - (gravity * hflow * local_time_factor * tempslope)) /
                                          (1 + gravity * hflow * local_time_factor * (temp_mannings * temp_mannings) * Math.Abs(qx[x, y]) /
                                          Math.Pow(hflow, (10 / 3))));
                                //if (oldqx != 0) qx[x, y] = (oldqx + qx[x, y]) / 2;

                                // need to have these lines to stop too much water moving from one cellt o another - resulting in -ve discharges
                                // whihc causes a large instability to develop - only in steep catchments really
                                if (qx[x, y] > 0 && (qx[x, y] / hflow) / Math.Sqrt(gravity * hflow) > froude_limit) qx[x, y] = hflow * (Math.Sqrt(gravity * hflow) * froude_limit);
                                if (qx[x, y] < 0 && Math.Abs(qx[x, y] / hflow) / Math.Sqrt(gravity * hflow) > froude_limit) qx[x, y] = 0 - (hflow * (Math.Sqrt(gravity * hflow) * froude_limit));

                                if (qx[x, y] > 0 && (qx[x, y] * local_time_factor / DX) > (water_depth[x, y] / 4)) qx[x, y] = ((water_depth[x, y] * DX) / 5) / local_time_factor;
                                if (qx[x, y] < 0 && Math.Abs(qx[x, y] * local_time_factor / DX) > (water_depth[x - 1, y] / 4)) qx[x, y] = 0 - ((water_depth[x - 1, y] * DX) / 5) / local_time_factor;

                                if (isSuspended[1])
                                {
                                    for (int T = 0; T <= tracers; T++)
                                    {
                                        if (qx[x, y] > 0) qxs[x, y, T] = qx[x, y] * (Vsusptot[x, y, T] / water_depth[x, y]);
                                        if (qx[x, y] < 0) qxs[x, y, T] = qx[x, y] * (Vsusptot[x - 1, y, T] / water_depth[x - 1, y]);

                                        if (qxs[x, y, T] > 0 && qxs[x, y, T] * local_time_factor > (Vsusptot[x, y, T] * DX) / 4) qxs[x, y, T] = ((Vsusptot[x, y, T] * DX) / 5) / local_time_factor;
                                        if (qxs[x, y, T] < 0 && Math.Abs(qxs[x, y, T] * local_time_factor) > (Vsusptot[x - 1, y, T] * DX) / 4) qxs[x, y, T] = 0 - ((Vsusptot[x - 1, y, T] * DX) / 5) / local_time_factor;
                                    }
                                }

                                // calc velocity now
                                if (qx[x, y] > 0) vel_dir[x, y, 7] = qx[x, y] / hflow;
                                if (qx[x, y] < 0) vel_dir[x - 1, y, 3] = (0 - qx[x, y]) / hflow;

                            }
                            else
                            {
                                qx[x, y] = 0;
                                for (int T = 0; T <= tracers; T++) qxs[x, y, T] = 0;
                            }
                        }

                        //routing in the y direction
                        if ((water_depth[x, y] > 0 || water_depth[x, y - 1] > 0) && elev[x, y - 1] > -9999)
                        {
                            double hflow = Math.Max(elev[x, y] + water_depth[x, y], elev[x, y - 1] + water_depth[x, y - 1]) -
                                            Math.Max(elev[x, y], elev[x, y - 1]);
                            if (hflow > DX / 2) hflow = DX / 2;

                            if (hflow > hflow_threshold)
                            {
                                double tempslope = (((elev[x, y - 1] + water_depth[x, y - 1])) -
                                    (elev[x, y] + water_depth[x, y])) / DX;
                                if (y == ymax) tempslope = edgeslope;
                                if (y <= 2) tempslope = 0 - edgeslope;

                                //double oldqy = qy[x, y];
                                qy_prev[x, y] = qy[x, y]; // OIL_V1 save q state
                                qy[x, y] = ((qy[x, y] - (gravity * hflow * local_time_factor * tempslope)) /
                                          (1 + gravity * hflow * local_time_factor * (temp_mannings * temp_mannings) * Math.Abs(qy[x, y]) /
                                          Math.Pow(hflow, (10 / 3))));
                                //if (oldqy != 0) qy[x, y] = (oldqy + qy[x, y]) / 2;

                                // need to have these lines to stop too much water moving from one cellt o another - resulting in -ve discharges
                                // whihc causes a large instability to develop - only in steep catchments really
                                if (qy[x, y] > 0 && (qy[x, y] / hflow) / Math.Sqrt(gravity * hflow) > froude_limit) qy[x, y] = hflow * (Math.Sqrt(gravity * hflow) * froude_limit);
                                if (qy[x, y] < 0 && Math.Abs(qy[x, y] / hflow) / Math.Sqrt(gravity * hflow) > froude_limit) qy[x, y] = 0 - (hflow * (Math.Sqrt(gravity * hflow) * froude_limit));

                                if (qy[x, y] > 0 && (qy[x, y] * local_time_factor / DX) > (water_depth[x, y] / 4)) qy[x, y] = ((water_depth[x, y] * DX) / 5) / local_time_factor;
                                if (qy[x, y] < 0 && Math.Abs(qy[x, y] * local_time_factor / DX) > (water_depth[x, y - 1] / 4)) qy[x, y] = 0 - ((water_depth[x, y - 1] * DX) / 5) / local_time_factor;


                                if (isSuspended[1])
                                {
                                    for (int T = 0; T <= tracers; T++)
                                    {
                                        if (qy[x, y] > 0) qys[x, y, T] = qy[x, y] * (Vsusptot[x, y, T] / water_depth[x, y]);
                                        if (qy[x, y] < 0) qys[x, y, T] = qy[x, y] * (Vsusptot[x, y - 1, T] / water_depth[x, y - 1]);

                                        if (qys[x, y, T] > 0 && qys[x, y, T] * local_time_factor > (Vsusptot[x, y, T] * DX) / 4) qys[x, y, T] = ((Vsusptot[x, y, T] * DX) / 5) / local_time_factor;
                                        if (qys[x, y, T] < 0 && Math.Abs(qys[x, y, T] * local_time_factor) > (Vsusptot[x, y - 1, T] * DX) / 4) qys[x, y, T] = 0 - ((Vsusptot[x, y - 1, T] * DX) / 5) / local_time_factor;
                                    }
                                }

                                // calc velocity now
                                if (qy[x, y] > 0) vel_dir[x, y, 1] = qy[x, y] / hflow;
                                if (qy[x, y] < 0) vel_dir[x, y - 1, 5] = (0 - qy[x, y]) / hflow;
                            }
                            else
                            {
                                qy[x, y] = 0;
                                for (int T = 0; T <= tracers; T++) qys[x, y, T] = 0;
                            }
                        }

                    }
                }
            });


        }

        void depth_update()
        {
            double local_time_factor = time_factor;
            if (local_time_factor > (courant_number * (DX / Math.Sqrt(gravity * (maxdepth))))) local_time_factor = courant_number * (DX / Math.Sqrt(gravity * (maxdepth)));
            double[] tempmaxdepth2;
            tempmaxdepth2 = new Double[ymax + 2];

            maxdepth = 0;

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                double tempmaxdepth = 0;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (isTraceWater == true || isSimulateTemperature == true) // for water tracing/temperature, we need to record the volumes of water moving between cells - MDW / TEMP_V1
                    {

                        dhdt_x[x + 1, y] = local_time_factor * qx[x + 1, y] / DX;
                        dhdt_x[x, y] = local_time_factor * qx[x, y] / DX;
                        dhdt_y[x, y + 1] = local_time_factor * qy[x, y + 1] / DX;
                        dhdt_y[x, y] = local_time_factor * qy[x, y] / DX;
                    }

                    // update water depths
                    water_depth[x, y] += local_time_factor * (qx[x + 1, y] - qx[x, y] + qy[x, y + 1] - qy[x, y]) / DX;
                    // now update SS concs
                    if (isSuspended[1])
                    {
                        for (int T = 0; T <= tracers; T++) Vsusptot[x, y, T] += local_time_factor * (qxs[x + 1, y, T] - qxs[x, y, T] + qys[x, y + 1, T] - qys[x, y, T]) / DX;
                    }

                    if (water_depth[x, y] > 0)
                    {
                        // line to remove any water depth on nodata cells (that shouldnt get there!)
                        if (elev[x, y] == -9999) water_depth[x, y] = 0;
                        // calc max flow depth for time step calc
                        if (water_depth[x, y] > tempmaxdepth) tempmaxdepth = water_depth[x, y];
                    }
                }
                tempmaxdepth2[y] = tempmaxdepth;
            });
            // reduction
            for (int y = 1; y <= ymax; y++) if (tempmaxdepth2[y] > maxdepth) maxdepth = tempmaxdepth2[y];
        }


        void save_tracer_states() // MDW 13/03/16
                                  // MDW_V2 updated to zero index
        {
            // Save previous states of water and source propotions, after the addition of inputs
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    water_depth_prev[x, y] = water_depth[x, y];
                    for (int z = 0; z < nSources; z++)
                    {
                        watertracer_prev[x, y, z] = watertracer[x, y, z];

                    }
                    if (isTraceRainZonation == true)
                    {
                        for (int z = 0; z < nRainZones; z++)
                        {
                            watertracerRainZone_prev[x, y, z] = watertracerRainZone[x, y, z];
                        }

                    }
                    if (isTraceSolutes == true) // MDW_V2
                    {
                        for (int z = 0; z < nSolutes; z++)
                        {
                            solutetracer_prev[x, y, z] = solutetracer[x, y, z];
                        }
                    }

                }
            });
        }

        // TEMP_V1 - saves the previous water_temp state, analogous to save_tracer_states().
        // Kept as its own function (rather than folded into save_tracer_states()) so that
        // running with isSimulateTemperature == true and isTraceWater == false never touches
        // the watertracer/nSources-dependent code in that function at all.
        void save_temperature_states()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    water_temp_prev[x, y] = water_temp[x, y];
                }
            });
        }

        void oil_area()
        {

            /*var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;*/

            count_cells = 0;

            for (int x = 1; x < xmax; x++)
            {
                for (int y = 1; y < ymax; y++)
                {

                    if (oil_depth[x, y] > 0.00001)
                    {
                        count_cells++;
                    }
                    oil_S = count_cells * DX * DX; // MDW : note to check : this will accumulate as the loop scans across the array. oil_S is a 2D array 

                }


            }//);
        }


        void oil_evaporation(double local_time_factor)

        {

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    /*for (int x = 1; x < xmax; x++)
                    {
                        for (int y = 1; y < ymax; y++)
                        {*/


                    if (oil_depth[x, y] > 0)
                    {
                        iteration_t += local_time_factor;
                        //evaporation rate - no wind influence from Fingas studies
                        //Ev = (3.24 + 0.054 * 15) * Math.Log(iteration_t/60)/100;

                        //evaporation exposure
                        oil_exp = oil_transf_coeff * oil_S * (local_time_factor / 60) / oil_totalv;

                        Ev = (oil_T / (oil_B * oil_Tg)) * Math.Log(oil_exp * (oil_B * oil_Tg / oil_T) * Math.Exp(oil_A - (oil_B * oil_To / oil_T)) + 1);

                    }


                }
            });
        }


        void oilroute(double local_time_factor)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (elev[x, y] > -9999)
                    {

                        // routing oil in x direction
                        if ((oil_depth[x, y] > 0 | oil_depth[x - 1, y] > 0) && elev[x - 1, y] > -9999) // assess cells containing oil
                        {
                            // max oil depth routing between cells
                            double oil_hflow = Math.Max(water_depth[x, y] + elev[x, y] + oil_depth[x, y], water_depth[x - 1, y] + elev[x - 1, y] + oil_depth[x - 1, y]) - Math.Max(elev[x, y] + water_depth[x, y], elev[x - 1, y] + water_depth[x - 1, y]);

                            //spreading - diffusion function in y direction
                            double oil_Dx = gravity * (oil_depth_init * oil_depth_init) * (ro_water - oil_density) * oil_density / ro_water / Cf;

                            // advection-dispersion equation x-direction
                            oil_hx_prev[x, y] = oil_hx[x, y];
                            oil_hx[x, y] = oil_hx_prev[x, y] - ( local_time_factor * (DX *oil_hflow * (qx[x, y]) / (DX * DX) - oil_Dx * (oil_hflow) / (DX * DX)));

                            // Adjust sign based on qx - force the oil in the same direction of water
                            if (qx[x, y] < 0)
                            {
                                oil_hx[x, y] = -(Math.Abs(oil_hx[x, y]));
                            }
                            else if (qx[x, y] >= 0)
                            {
                                oil_hx[x, y] = (Math.Abs(oil_hx[x, y]));
                            }


                        }

                        // routing in y direction
                        if ((oil_depth[x, y] > 0 | oil_depth[x, y - 1] > 0) && elev[x, y - 1] > -9999)
                        {
                            // max oil depth routing between cells
                            double oil_hflow = Math.Max(water_depth[x, y] + elev[x, y] + oil_depth[x, y], water_depth[x, y - 1] + elev[x, y - 1] + oil_depth[x, y - 1]) - Math.Max(elev[x, y] + water_depth[x, y], elev[x, y - 1] + water_depth[x, y - 1]);

                            //spreading - diffusion function in y direction

                            double oil_Dy = gravity * (oil_depth_init * oil_depth_init) * (ro_water - oil_density) * oil_density / ro_water / Cf;
                            // advection-dispersion equation y-direction
                            oil_hy_prev[x, y] = oil_hy[x, y];
                            oil_hy[x, y] = (oil_hy_prev[x, y] - ( local_time_factor * (DX *oil_hflow * (qy[x, y]) / (DX * DX) - oil_Dy * (oil_hflow) / (DX * DX))));

                            // Adjust sign based on qy - force the oil in the same direction of water
                            if (qy[x, y] < 0)
                            {
                                oil_hy[x, y] = -(Math.Abs(oil_hy[x, y]));
                            }
                            else if (qy[x, y] >= 0)
                            {
                                oil_hy[x, y] = (Math.Abs(oil_hy[x, y]));
                            }


                        }



                    }
                }
            });
        }

        void oil_update()

        {

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (elev[x, y] > -9999)
                    {
                        oil_depth_prev[x, y] = oil_depth[x, y];
                        oil_depth[x, y] += (oil_hx[x + 1, y] - oil_hx[x, y] + oil_hy[x, y + 1] - oil_hy[x, y]);

                        if (oil_depth[x, y] > 0)
                        {
                            oil_evap_fraction[x, y] = Ev * oil_depth[x, y] / count_cells;
                            oil_depth[x, y] -= oil_evap_fraction[x, y];

                        }

                        if (oil_depth[x, y] < 0)
                        {
                            error_negative = Math.Abs(oil_depth[x, y] * DX * DX);
                            error_negative_sum += error_negative;
                            oil_depth[x, y] = 0;
                        }

                        // oil concentration

                        oil_conc[x, y] = (((oil_depth[x, y] * ro_water) / (oil_depth[x, y] * oil_density + water_depth[x, y] * ro_water))) / 1000; // g/L
                          
                        if (oil_conc[x, y] > 0)
                        {
                            oil_concentration[x, y] = oil_conc[x, y];
                        }

                    }
                }
            });
        }


        void oil_global_mass_balance()
        {

            oil_evap_t = 0;
            //oil_out_sum = 0;
            //oil_evap_sum = 0;
            //error_negative_sum = 0;


            for (int x = 1; x < xmax; x++)
            {
                for (int y = 1; y < ymax; y++)
                {
                    oil_evap_t += DX * DX * oil_evap_fraction[x, y];
                    //Vout += Vout_1 + Vout_2 + Vout_3 + Vout_4;
                }

            }

            //error_negative_sum += Math.Abs(error_negative);

            oil_evap_sum += oil_evap_t;
            oil_in_sum = oil_totalv - Vout_total - oil_evap_sum;



        }


        void update_tracer_states()
        {
            ///////////////////////////////////////////////////////////////////
            // Update water proportions for water source tracing - MDW 13/03/16
            // Note - we only need to deal with inflows for each cell as it is
            // assumed that the water in a cell is mixed, so the propotion from
            // each source in outflow will be the same as in the cell itself.
            //
            // 1. Get depth after outflows only - check not to get -ve depths
            // 2. Get dhdt added by each inflow and scale for each water source
            // 3. In main cell, work out the sum of depth from each source:
            //    : the proportion of remaining water from each source, plus
            //    : the sum of dhdt from each source
            // 4. Update the proportions from each source: divide by the new depth

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {

                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (water_depth[x, y] > 0.0) // assess all cells which contain water
                    {
                        double dhdt_sumOut, dhdt_sumIn, depth_after_outflows, raindepth_after_outflows, raindepth;
                        double[] dhdt_sumInSrc;
                        dhdt_sumInSrc = new Double[nSources + 1];
                        double[] dhdt_sumInSrcRainZone = new Double[nRainZones + 1];
                        double[] dhdt_sumInSrcSolutes = new Double[nSolutes];
                        //double solute_after_outflows;

                        // work out total outflows for this cell - source not important here
                        dhdt_sumOut = 0.0;
                        if (dhdt_x[x + 1, y] < 0.0) { dhdt_sumOut += dhdt_x[x + 1, y]; }  // Flow from right: -ve = outflow
                        if (dhdt_x[x, y] > 0.0) { dhdt_sumOut -= dhdt_x[x, y]; }      // Flow from left:  +ve = outflow
                        if (dhdt_y[x, y + 1] < 0.0) { dhdt_sumOut += dhdt_y[x, y + 1]; }  // Flow from up:    -ve = outflow
                        if (dhdt_y[x, y] > 0.0) { dhdt_sumOut -= dhdt_y[x, y]; }      // Flow from down:  +ve = outflow
                                                                                      // n.b. sum of outflows will be negative

                        // get depth of cell from previous iteration after outflows
                        depth_after_outflows = water_depth_prev[x, y] + dhdt_sumOut;

                        for (int src = 0; src < nSources; src++) // MDW_V2 updated to zero index
                        {
                            dhdt_sumInSrc[src] = 0.0;

                            // work out the total inflow from neighbouring cells for this source of water - ignore outflows
                            if (dhdt_x[x + 1, y] > 0.0) { dhdt_sumInSrc[src] += dhdt_x[x + 1, y] * watertracer_prev[x + 1, y, src]; }  // Flow from right: +ve = inflow
                            if (dhdt_x[x, y] < 0.0) { dhdt_sumInSrc[src] -= dhdt_x[x, y] * watertracer_prev[x - 1, y, src]; }  // Flow from left:  -ve = inflow
                            if (dhdt_y[x, y + 1] > 0.0) { dhdt_sumInSrc[src] += dhdt_y[x, y + 1] * watertracer_prev[x, y + 1, src]; }  // Flow from up:    +ve = inflow
                            if (dhdt_y[x, y] < 0.0) { dhdt_sumInSrc[src] -= dhdt_y[x, y] * watertracer_prev[x, y - 1, src]; }  // Flow from down:  -ve = inflow

                            // update sources at this location
                            if ((dhdt_sumInSrc[src] == 0.0) & (watertracer_prev[x, y, src] == 0.0)) // if there is no contribution or existing water from this source
                            {
                                watertracer[x, y, src] = 0.0;
                            }
                            else // if there are inflows or existing water from this source, update proportions
                            {
                                if (depth_after_outflows < 0) // just in case
                                {
                                    watertracer[x, y, src] = dhdt_sumInSrc[src] / (water_depth[x, y] - depth_after_outflows); // proportions are assigned based only on incoming water
                                }
                                else
                                {
                                    // ([depth of water from this source still in cell] + [depth of water from this source flowing in]) / [total depth now in cell]
                                    watertracer[x, y, src] = ((depth_after_outflows * watertracer_prev[x, y, src]) + dhdt_sumInSrc[src]) / water_depth[x, y];
                                }
                            }

                        }
                        // deal with rain zones - MDW 01/04/16
                        if (isTraceRainZonation == true && watertracer[x, y, 1] > 0.0)
                        {
                            for (int src = 0; src < nRainZones; src++) // MDW_V2 updated to zero index
                            {
                                dhdt_sumInSrcRainZone[src] = 0.0;

                                // work out the total inflow from neighbouring cells for this source of water - ignore outflows
                                if (dhdt_x[x + 1, y] > 0.0) { dhdt_sumInSrcRainZone[src] += dhdt_x[x + 1, y] * watertracer_prev[x + 1, y, 1] * watertracerRainZone_prev[x + 1, y, src]; }  // Flow from right: +ve = inflow
                                if (dhdt_x[x, y] < 0.0) { dhdt_sumInSrcRainZone[src] -= dhdt_x[x, y] * watertracer_prev[x - 1, y, 1] * watertracerRainZone_prev[x - 1, y, src]; }  // Flow from left:  -ve = inflow
                                if (dhdt_y[x, y + 1] > 0.0) { dhdt_sumInSrcRainZone[src] += dhdt_y[x, y + 1] * watertracer_prev[x, y + 1, 1] * watertracerRainZone_prev[x, y + 1, src]; }  // Flow from up:    +ve = inflow
                                if (dhdt_y[x, y] < 0.0) { dhdt_sumInSrcRainZone[src] -= dhdt_y[x, y] * watertracer_prev[x, y - 1, 1] * watertracerRainZone_prev[x, y - 1, src]; }  // Flow from down:  -ve = inflow

                                // update sources at this location
                                if ((dhdt_sumInSrcRainZone[src] == 0.0) & (watertracerRainZone_prev[x, y, src] == 0.0)) // if there is no contribution or existing water from this source
                                {
                                    watertracerRainZone[x, y, src] = 0.0;
                                }
                                else // if there are inflows or existing water from this source, update proportions
                                {
                                    if (depth_after_outflows < 0) // just in case
                                    {
                                        watertracerRainZone[x, y, src] = dhdt_sumInSrcRainZone[src] / (water_depth[x, y] - depth_after_outflows); // proportions are assigned based only on incoming water
                                    }
                                    else
                                    {
                                        // ([rain depth from this zone still in cell] + [rain zone source flowing in])/ [total rain depth now in cell]
                                        raindepth_after_outflows = depth_after_outflows * watertracer_prev[x, y, 1];
                                        raindepth = water_depth[x, y] * watertracer[x, y, 1];
                                        if (raindepth > 0.0) watertracerRainZone[x, y, src] = ((raindepth_after_outflows * watertracerRainZone_prev[x, y, src]) + dhdt_sumInSrcRainZone[src]) / raindepth;
                                        else watertracerRainZone[x, y, src] = 0;
                                    }
                                }

                            }
                        }
                        // update solute tracers - MDW_V2
                        if (isTraceSolutes == true)
                        {
                            // MDW_Apr24: Reworked algorithm due to very large values accumulating, caused by division by very small depths.

                            // work out the total inflow from neighbouring cells for all sources. Ignoring outflow. This is needed
                            // for depth-averaging the solute amounts
                            dhdt_sumIn = 0.0;
                            if (dhdt_x[x + 1, y] > 0.0) { dhdt_sumIn += dhdt_x[x + 1, y]; }  // Flow from right: +ve = inflow
                            if (dhdt_x[x, y] < 0.0) { dhdt_sumIn -= dhdt_x[x, y]; }  // Flow from left:  -ve = inflow
                            if (dhdt_y[x, y + 1] > 0.0) { dhdt_sumIn += dhdt_y[x, y + 1]; }  // Flow from up:    +ve = inflow
                            if (dhdt_y[x, y] < 0.0) { dhdt_sumIn -= dhdt_y[x, y]; }  // Flow from down:  -ve = inflow

                            for (int src = 0; src < nSolutes; src++)
                            {
                                dhdt_sumInSrcSolutes[src] = 0.0;
                                if (dhdt_sumIn > 0.0)
                                {
                                    // for each neighbouring cell, find the volume of this solute flowing from that cell: [total in that cell] * [dhdt from that cell]
                                    // get depth weighted average of inputs from source cells = [total solute input]
                                    dhdt_sumInSrcSolutes[src] = 0.0;

                                    // work out the total solute*dhdt from neighbouring cells for this source of water - ignore outflows
                                    if (dhdt_x[x + 1, y] > 0.0) { dhdt_sumInSrcSolutes[src] += dhdt_x[x + 1, y] * solutetracer_prev[x + 1, y, src]; }  // Flow from right: +ve = inflow
                                    if (dhdt_x[x, y] < 0.0) { dhdt_sumInSrcSolutes[src] -= dhdt_x[x, y] * solutetracer_prev[x - 1, y, src]; }  // Flow from left:  -ve = inflow
                                    if (dhdt_y[x, y + 1] > 0.0) { dhdt_sumInSrcSolutes[src] += dhdt_y[x, y + 1] * solutetracer_prev[x, y + 1, src]; }  // Flow from up:    +ve = inflow
                                    if (dhdt_y[x, y] < 0.0) { dhdt_sumInSrcSolutes[src] -= dhdt_y[x, y] * solutetracer_prev[x, y - 1, src]; }  // Flow from down:  -ve = inflow

                                    // MDW_Apr24: get weighted average of solutes flowing into this cell, ignore outflows [total_solute_input]:
                                    dhdt_sumInSrcSolutes[src] = dhdt_sumInSrcSolutes[src] / dhdt_sumIn;
                                }

                                // update sources at this location
                                if ((dhdt_sumInSrcSolutes[src] == 0.0) & (solutetracer_prev[x, y, src] == 0.0)) // if there is no contribution or existing solute from this source
                                {
                                    solutetracer[x, y, src] = 0.0;
                                }
                                else if ((dhdt_sumInSrcSolutes[src] == 0.0) & dhdt_sumOut < 0.0) // for no additions, solute concentration stays the same
                                {
                                    solutetracer[x, y, src] = solutetracer_prev[x, y, src];
                                }
                                else // if there are solute inflows or existing solute from this source, update amounts
                                {
                                    if (depth_after_outflows < 0 || water_depth_prev[x, y] == 0)  // cell empty, so solute amount = solutes from inflow
                                    {
                                        solutetracer[x, y, src] = dhdt_sumInSrcSolutes[src];
                                    }
                                    else
                                    {
                                        // updated solute amount is a depth weighted average of what was there and what is flowing in
                                        solutetracer[x, y, src] = ((solutetracer_prev[x, y, src] * depth_after_outflows) +  // solute already there
                                                                   (dhdt_sumInSrcSolutes[src] * dhdt_sumIn))                // solute flowing in
                                                                   / water_depth[x, y];
                                    }
                                }
                            }
                        }

                        // MDW_DEBUG
                        // check for error
                        /* double tracersum = 0.0;
                        for (int src = 0; src <= nSources; src++)
                        {
                            tracersum += watertracer[x, y, src];
                            if (tracersum > 1.0000001)
                            {
                                MessageBox.Show("Tracer exception caught: tracersum = " + Convert.ToString(tracersum) +
                                    ", counter = " + Convert.ToString(counter) + ", cycle = " + Convert.ToString(cycle));
                            }
                        } */
                    }
                }
            });


        }


        // TEMP_V1 - water temperature module functions (see spec doc for equations/logic)

        // TEMP_V1 - returns the given water temperature, or the background initial value
        // if it's the nodata sentinel (e.g. donor cell has no established temperature yet).
        double GetTempOrDefault(double t)
        {
            return (t == -9999) ? waterTempInitialValue : t;
        }

        // TEMP_V1 - fine-cadence advective mixing of water temperature, run every hydraulic
        // iteration. Mirrors the solute-tracer update logic in update_tracer_states() (depth-
        // weighted average of an intensive quantity), not the water-source-tracer proportion
        // logic. NOTE: this only handles cell-to-cell mixing via qx/qy fluxes (dhdt_x/dhdt_y).
        // It does NOT yet assign a temperature to water newly added at reach/catchment/tidal
        // input points - that is a separate, still-outstanding integration point (see A8a).
        void update_water_temperature_advection(double local_time_factor)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (water_depth[x, y] > 0.0)
                    {
                        // Outflows (source not important - just total volume leaving)
                        double dhdt_sumOut = 0.0;
                        if (dhdt_x[x + 1, y] < 0.0) { dhdt_sumOut += dhdt_x[x + 1, y]; }
                        if (dhdt_x[x, y] > 0.0) { dhdt_sumOut -= dhdt_x[x, y]; }
                        if (dhdt_y[x, y + 1] < 0.0) { dhdt_sumOut += dhdt_y[x, y + 1]; }
                        if (dhdt_y[x, y] > 0.0) { dhdt_sumOut -= dhdt_y[x, y]; }

                        double depth_after_outflows = water_depth_prev[x, y] + dhdt_sumOut;

                        // Total inflow volume (ignoring outflows)
                        double dhdt_sumIn = 0.0;
                        if (dhdt_x[x + 1, y] > 0.0) { dhdt_sumIn += dhdt_x[x + 1, y]; }
                        if (dhdt_x[x, y] < 0.0) { dhdt_sumIn -= dhdt_x[x, y]; }
                        if (dhdt_y[x, y + 1] > 0.0) { dhdt_sumIn += dhdt_y[x, y + 1]; }
                        if (dhdt_y[x, y] < 0.0) { dhdt_sumIn -= dhdt_y[x, y]; }

                        double dhdt_sumInTemp = 0.0;
                        if (dhdt_sumIn > 0.0)
                        {
                            // depth-weighted average temperature of the water flowing in,
                            // defaulting any nodata donor to the background initial value
                            if (dhdt_x[x + 1, y] > 0.0) dhdt_sumInTemp += dhdt_x[x + 1, y] * GetTempOrDefault(water_temp_prev[x + 1, y]);
                            if (dhdt_x[x, y] < 0.0) dhdt_sumInTemp -= dhdt_x[x, y] * GetTempOrDefault(water_temp_prev[x - 1, y]);
                            if (dhdt_y[x, y + 1] > 0.0) dhdt_sumInTemp += dhdt_y[x, y + 1] * GetTempOrDefault(water_temp_prev[x, y + 1]);
                            if (dhdt_y[x, y] < 0.0) dhdt_sumInTemp -= dhdt_y[x, y] * GetTempOrDefault(water_temp_prev[x, y - 1]);
                            dhdt_sumInTemp = dhdt_sumInTemp / dhdt_sumIn;
                        }

                        if (dhdt_sumIn == 0.0 && water_temp_prev[x, y] == -9999)
                        {
                            // no inflow, and this cell had no established temperature - stays nodata
                            water_temp[x, y] = -9999;
                        }
                        else if (dhdt_sumIn == 0.0 && dhdt_sumOut < 0.0)
                        {
                            // outflow only, no new inflow - temperature of remaining water unchanged
                            water_temp[x, y] = GetTempOrDefault(water_temp_prev[x, y]);
                        }
                        else if (depth_after_outflows <= 0.0 || water_depth_prev[x, y] <= 0.0 || water_temp_prev[x, y] == -9999)
                        {
                            // cell was empty (or had no established temperature) before this
                            // step - new temperature is simply the inflow average (A2)
                            water_temp[x, y] = dhdt_sumInTemp;
                        }
                        else
                        {
                            // depth-weighted average of what was already there and what's flowing in
                            water_temp[x, y] = ((water_temp_prev[x, y] * depth_after_outflows) + (dhdt_sumInTemp * dhdt_sumIn)) / water_depth[x, y];
                        }
                    }
                    else
                    {
                        water_temp[x, y] = -9999; // TEMP_V1 - cell is dry: nodata (A2)
                    }
                }
            });
        }

        // TEMP_V1 - coarse-cadence surface energy balance (full or simplified scheme),
        // run on the thermal_time schedule (see erodedepo()).
        void update_water_temperature_energybalance()
        {
            double dt_seconds = thermal_update_interval * 60;
            double rho_w = 1000.0;   // kg/m3, constant (A18 - see spec doc)
            double Cpw = 4186.0;     // J/(kg.C)

            if (useSimplifiedTempScheme == true)
            {
                // TEMP_V1 - simplified (equilibrium temperature) scheme, HEC-RAS Eqs. 2.17-2.21.
                // Per-zone met values are interpolated once here, then indexed by zone per cell,
                // rather than re-interpolating for every cell (see interpolate_met() note, Step 12).
                double[] shortwave_zone = new double[nMetZones];
                double[] dewpoint_zone = new double[nMetZones];
                double[] wind7_zone = new double[nMetZones];

                for (int zn = 0; zn < nMetZones; zn++)
                {
                    shortwave_zone[zn] = interpolate_met(hourly_shortwave, cycle, zn);
                    dewpoint_zone[zn] = interpolate_met(hourly_dewpoint, cycle, zn);
                    double windAtMeasHeight = interpolate_met(hourly_windspeed, cycle, zn);
                    wind7_zone[zn] = wind_speed_at_height(windAtMeasHeight, windMeasurementHeight, 7.0);
                }

                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
                Parallel.For(1, ymax + 1, options, delegate (int y)
                {
                    int inc = 1;
                    while (down_scan[y, inc] > 0)
                    {
                        int x = down_scan[y, inc];
                        inc++;

                        if (water_depth[x, y] > water_depth_erosion_threshold)
                        {
                            int zone = met_zonation[x, y];
                            if (zone < 0 || zone >= nMetZones) zone = 0;

                            double q_sw = shortwave_zone[zone];
                            double T_d = dewpoint_zone[zone];
                            double u_w7 = wind7_zone[zone];
                            double T_w = water_temp[x, y];

                            double f_uw7 = 9.2 + 0.46 * u_w7 * u_w7;                         // Eq. 2.19
                            double T_bar = (T_w + T_d) / 2.0;
                            double beta = 0.35 + 0.015 * T_bar + 0.0012 * T_bar * T_bar;      // Eq. 2.20
                            double K_T = 4.5 + 0.05 * T_w + (beta + 0.47) * f_uw7;            // Eq. 2.18

                            if (K_T <= 0) K_T = 0.0001; // guard against a degenerate/negative exchange coefficient

                            double T_eq = T_d + q_sw / K_T;                                   // Eq. 2.21

                            double depth = water_depth[x, y];
                            if (depth < water_depth_erosion_threshold) depth = water_depth_erosion_threshold;

                            double decay = Math.Exp(-K_T * dt_seconds / (rho_w * Cpw * depth));
                            double T_new = T_eq + (T_w - T_eq) * decay;

                            if (T_new < 0) T_new = 0; // TEMP_V1 - ice deferred (A15): floor at 0 C

                            water_temp[x, y] = T_new;
                        }
                    }
                });
            }
            else
            {
                // TEMP_V1 - full energy balance scheme. To be implemented (next step).
                return;
            }
        }


        // TEMP_V1 - solar altitude angle (radians above the horizon), using standard solar-
        // position equations (Spencer 1971 declination series, standard equation-of-time
        // correction, hour angle from apparent solar time). This is a widely-used, well-
        // established formulation (e.g. Duffie & Beckman, "Solar Engineering of Thermal
        // Processes") rather than a HEC-RAS-specific one - the report itself doesn't fully
        // specify its own solar-position derivation in the material reviewed, so this is our
        // best-practice choice for that piece. Uses siteLatitude/siteLongitude/siteTimeZone.
        double solar_altitude(double dayOfYear, double hourOfDay)
        {
            double gamma = 2.0 * Math.PI * (dayOfYear - 1) / 365.0; // day angle, radians

            // Spencer (1971) solar declination series, radians
            double declination = 0.006918
                - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma)
                - 0.006758 * Math.Cos(2 * gamma) + 0.000907 * Math.Sin(2 * gamma)
                - 0.002697 * Math.Cos(3 * gamma) + 0.001480 * Math.Sin(3 * gamma);

            // Equation of time, minutes
            double eqTime = 229.18 * (0.000075
                + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma)
                - 0.014615 * Math.Cos(2 * gamma) - 0.040849 * Math.Sin(2 * gamma));

            double standardMeridian = 15.0 * siteTimeZone; // degrees
            double timeCorrectionMin = 4.0 * (siteLongitude - standardMeridian) + eqTime;
            double solarTimeHours = hourOfDay + (timeCorrectionMin / 60.0);

            double hourAngleDeg = 15.0 * (solarTimeHours - 12.0);
            double hourAngleRad = hourAngleDeg * Math.PI / 180.0;
            double latRad = siteLatitude * Math.PI / 180.0;

            double sinAltitude = Math.Sin(latRad) * Math.Sin(declination)
                + Math.Cos(latRad) * Math.Cos(declination) * Math.Cos(hourAngleRad);

            if (sinAltitude > 1.0) sinAltitude = 1.0;
            if (sinAltitude < -1.0) sinAltitude = -1.0;

            return Math.Asin(sinAltitude); // radians; negative = sun below horizon
        }

        // TEMP_V1 - extraterrestrial radiation on a horizontal surface, q_o (HEC-RAS Eq. 2.5).
        double solar_geometry(double dayOfYear, double hourOfDay)
        {
            const double Q0 = 1360.0; // solar constant, W/m2

            double gamma = 2.0 * Math.PI * (dayOfYear - 1) / 365.0;
            // Earth-sun distance correction (1/r^2), Spencer (1971)
            double inv_r2 = 1.000110
                + 0.034221 * Math.Cos(gamma) + 0.001280 * Math.Sin(gamma)
                + 0.000719 * Math.Cos(2 * gamma) + 0.000077 * Math.Sin(2 * gamma);

            double altitude = solar_altitude(dayOfYear, hourOfDay);
            double sinAltitude = Math.Sin(altitude);
            if (sinAltitude < 0) sinAltitude = 0; // sun below horizon: no extraterrestrial radiation

            return Q0 * inv_r2 * sinAltitude;
        }

        // TEMP_V1 - R_s, HEC-RAS solar-altitude-dependent reflection coefficient.
        // Based on the widely-used Anderson (1954) clear-sky reflectivity curve (as tabulated
        // in, e.g., water-quality modelling references such as QUAL2E) - a decreasing power-law
        // function of solar altitude, since reflection off water rises sharply at low sun
        // angles. Flagged for a precise cross-check against the source report's own table if
        // exact reproduction of HEC-RAS results is required.
        double reflection_coefficient(double solarAltitudeRad)
        {
            double altitudeDeg = solarAltitudeRad * 180.0 / Math.PI;

            if (altitudeDeg <= 0) return 1.0; // sun at/below horizon: not physically meaningful, but avoids a divide-by-zero/negative-power issue downstream

            double Rs = 1.18 * Math.Pow(altitudeDeg, -0.77);

            if (Rs > 1.0) Rs = 1.0;   // physical bound
            if (Rs < 0.03) Rs = 0.03; // floor, consistent with typical clear-water minimum reflectivity near solar noon

            return Rs;
        }


        // Returns Ri, Richardson number (HEC-RAS Eq. 2.13). To be implemented.
        double richardson_number(double airTemp, double waterTemp, double windSpeed)
        {
            return 0.0;
        }

        // Returns f(u_s), wind function (HEC-RAS Eq. 2.11). To be implemented.
        double wind_function(double windSpeed, double Ri)
        {
            return 0.0;
        }

        // TEMP_V1 - converts elapsed model time (cycle, in minutes) plus the user-specified
        // simulation start date/time into a day-of-year and hour-of-day, for use in solar
        // geometry calculations. Uses .NET DateTime arithmetic to correctly handle month/
        // year rollovers and leap years rather than manual day-counting.
        void get_day_and_hour(double cycleMinutes, out double dayOfYear, out double hourOfDay)
        {
            DateTime current = simulationStartDateTime.AddMinutes(cycleMinutes);
            dayOfYear = current.DayOfYear;
            hourOfDay = current.Hour + (current.Minute / 60.0) + (current.Second / 3600.0);
        }

        const double stefanBoltzmann = 5.670e-8; // W/(m2.K4)

        // TEMP_V1 - cloud-cover exponent used in both the shortwave (HEC-RAS Eq. 2.4, confirmed
        // as squared) and atmospheric longwave (Eq. 2.6, ambiguous in the OCR'd source text
        // between linear and squared) cloud-correction terms. Named here rather than hardcoded
        // inline so it's trivial to correct in one place if checked against the original report
        // and found to differ for the longwave term specifically.
        const double longwaveCloudExponent = 2.0;

        // TEMP_V1 - atmospheric (downwelling) longwave radiation, q_atm, HEC-RAS default
        // (Eq. 2.6): Swinbank (1963) clear-sky formula with a cloud-cover correction.
        // No humidity term - see atmospheric_longwave_humidity() for the CAESAR-extension
        // alternative. Returns W/m2 (always positive - a gain to the water surface).
        double atmospheric_longwave_hecras(double airTempC, double cloudCoverFrac)
        {
            double Ta_K = airTempC + 273.15;
            double cloudTerm = 1.0 + 0.17 * Math.Pow(cloudCoverFrac, longwaveCloudExponent);
            return 0.937e-5 * cloudTerm * stefanBoltzmann * Math.Pow(Ta_K, 6);
        }

        // TEMP_V1 - simple Magnus-Tetens saturation vapour pressure (mb), used only by
        // atmospheric_longwave_humidity() below. Deliberately kept separate from the
        // saturation_vapour_pressure() stub (Step 3), which is reserved for HEC-RAS's own
        // Eq. 2.9 polynomial, needed later for the latent heat term - that one should match
        // the source report exactly; this one is a standard, independent approximation only
        // used by our own humidity-aware longwave extension.
        double saturation_vapour_pressure_magnus(double tempC)
        {
            return 6.1094 * Math.Exp(17.625 * tempC / (tempC + 243.04));
        }

        // TEMP_V1 - atmospheric longwave, CAESAR-extension alternative: humidity-aware
        // clear-sky emissivity (Idso, 1981), combined with the same cloud-cover correction
        // used in the HEC-RAS default, for direct comparability between the two options.
        // Returns W/m2.
        double atmospheric_longwave_humidity(double airTempC, double cloudCoverFrac, double relativeHumidityPercent)
        {
            double Ta_K = airTempC + 273.15;
            double es_air = saturation_vapour_pressure_magnus(airTempC);
            double ea = (relativeHumidityPercent / 100.0) * es_air;

            double epsilon_clear = 0.70 + 5.95e-5 * ea * Math.Exp(1500.0 / Ta_K);
            double cloudTerm = 1.0 + 0.17 * Math.Pow(cloudCoverFrac, longwaveCloudExponent);

            return epsilon_clear * cloudTerm * stefanBoltzmann * Math.Pow(Ta_K, 4);
        }

        // TEMP_V1 - back (water) longwave emission, q_b, HEC-RAS Eq. 2.7. Fixed emissivity
        // 0.97 - no alternative option, per the spec (this term is well-constrained
        // physically and isn't user-configurable). Returns W/m2 (always positive - a loss
        // from the water surface).
        double water_longwave(double waterTempC)
        {
            double Tw_K = waterTempC + 273.15;
            return 0.97 * stefanBoltzmann * Math.Pow(Tw_K, 4);
        }

        // TEMP_V1 - log-law wind speed correction from measurement height to a target
        // reference height. Roughness length z0 depends on the measured wind speed itself
        // (HEC-RAS convention: 0.001 m if windSpeed < 2.3 m/s, else 0.015 m).
        double wind_speed_at_height(double windSpeedMeasured, double measurementHeight, double targetHeight)
        {
            if (windSpeedMeasured <= 0) return 0;
            if (measurementHeight <= 0) return windSpeedMeasured; // guard against a zero/invalid height input

            double z0 = (windSpeedMeasured < 2.3) ? 0.001 : 0.015;
            return windSpeedMeasured * Math.Log(targetHeight / z0) / Math.Log(measurementHeight / z0);
        }

        // Returns e_s, saturation vapour pressure (HEC-RAS Eq. 2.9). To be implemented.
        double saturation_vapour_pressure(double tempC)
        {
            return 0.0;
        }

        // Returns L, latent heat of vaporisation (temperature-dependent). To be implemented.
        double latent_heat_of_vaporisation(double tempC)
        {
            return 0.0;
        }

        // TEMP_V1 - linear time interpolation of a met variable at the current cycle,
        // for a given zone. Mirrors the interpolation style used in
        // reach_water_and_sediment_input() for hydrograph inputs.
        // NOTE: hourly_* arrays are zero-indexed by row (row 0 = first time step),
        // unlike the 1-indexed hourly_rain_data/hourly_m_value convention (see load_met_file()).
        double interpolate_met(double[,] metArray, double cycleMinutes, int zone)
        {
            int maxIdx = metArray.GetLength(0) - 1;
            int nZones = metArray.GetLength(1);

            if (zone < 0) zone = 0;
            if (zone >= nZones) zone = nZones - 1;

            int idx0 = (int)(cycleMinutes / met_data_time_step);
            if (idx0 < 0) idx0 = 0;
            if (idx0 >= maxIdx) idx0 = maxIdx - 1; // leave room for idx0+1
            if (idx0 < 0) idx0 = 0; // safety if the array only has one row

            int idx1 = idx0 + 1;
            if (idx1 > maxIdx) idx1 = maxIdx;

            double value0 = metArray[idx0, zone];
            double value1 = metArray[idx1, zone];

            double proportion_between_time1and2 = (((idx0 + 1) * met_data_time_step) - cycleMinutes) / met_data_time_step;

            return value0 + ((value1 - value0) * (1 - proportion_between_time1and2));
        }


        void initialise_oil_simulation() // OIL_V1
        {

            // Read in input cell coordinates, checking for valid values. Disable oil spill and skip rest if parse fails
            if (!int.TryParse(OilYmin.Text, out oil_fromy) |
                !int.TryParse(OilYmax.Text, out oil_toy) |
                !int.TryParse(OilXmin.Text, out oil_fromx) |
                !int.TryParse(OilXmax.Text, out oil_tox))
            {
                MessageBox.Show("Oil spill simulation: invalid values found in location of cell inputs. Disabling.");
                isOilSimulation = false;
                return;
            }

            // Check for out of bound input cells
            if (Math.Min(oil_fromy, oil_toy) < 0 |
                Math.Max(oil_fromy, oil_toy) > ymax |
                Math.Min(oil_fromx, oil_tox) < 0 |
                Math.Max(oil_fromx, oil_tox) > xmax)
            {
                MessageBox.Show("Oil spill simulation: input locations are out of bounds. Disabling.");
                isOilSimulation = false;
                return;
            }

            // Read in timings/ volumes, checking for valid values. Disable oil spill and skip rest if parse fails
            if (!double.TryParse(OilTimeMin.Text, out oil_startt) |
                !double.TryParse(OilDepthStart.Text, out oil_starth) |
                !double.TryParse(OilVolume.Text, out oil_totalv) |
                !double.TryParse(OilSpillDuration.Text, out oil_spillt))
            {
                MessageBox.Show("Oil spill simulation: invalid values found in start/ minimum depth/ volume/ duration inputs. Disabling.");
                isOilSimulation = false;
                return;
            }
            oil_startt /= 60; // convert to minutes, same unit as time counter

            // Check volume, duration:  zero or negative leads to disabling oil simulation
            if (oil_totalv <= 0 | oil_spillt <= 0)
            {
                MessageBox.Show("Oil spill simulation: volume and/or duration is zero or negative. Disabling.");
                isOilSimulation = false;
                return;
            }

            // Check start time: negative values is invalid
            if (oil_startt < 0)
            {
                MessageBox.Show("Oil spill simulation: start time is negative. Disabling.");
                isOilSimulation = false;
                return;
            }

            // Check start time against length of simulation
            if (oil_startt > (maxcycle * 60))
            {
                MessageBox.Show("Oil spill simulation: start time is set to after the end of the simulation, so the spill will never occur. Disabling.");
                isOilSimulation = false;
                return;
            }

            // Check depth threshold: minimum default value
            if (oil_starth < 0.01)
            {
                MessageBox.Show("Oil spill simulation: depth threshold must be at least 0.01 m. Updating and continuing.");
                oil_starth = 0.01;
            }

            // All ok at this point

            // Initialise the constant spill rate
            oil_ratev = oil_totalv / oil_spillt;

            // initialise the input number of cells, taking account of a 1x1 input
            oil_n_cells = Math.Max(Math.Max(oil_fromx, oil_tox) - Math.Min(oil_fromx, oil_tox), 1) *
                          Math.Max(Math.Max(oil_fromy, oil_toy) - Math.Min(oil_fromy, oil_toy), 1);

            // Initialise other parameters - to be added to interface
            oil_density = 0.839; //then to be defined in the graphical interface input
            ro_water = 0.999;
            cinematic_v = 9;
            oil_T = 298;
            vel_wind = 1;
            //Initialise parameters for oil_evaporation
            oil_A = 6.3;
            oil_B = 10.3;
            Cf = 0.02;
            API = 141.5 / (oil_density / ro_water) - 131.5;

            //Crude oil
            //oil_To = 457 - 3.3447 * API;
            //oil_Tg = 1356.7 - 247.36 * Math.Log(API);

            //refined oil
            oil_To = (645.45 - 4.6588 * API);
            oil_Tg = (388.19 - 3.8725 * API);

            // wind to be considered variable in the future
            //oil_transf_coeff = 0.0107 * Math.Pow(vel_wind, 0.78) * Math.Pow(DX, -0.11) * Math.Pow(0.6, -0.67);
            oil_transf_coeff = 0.0025 * Math.Pow(vel_wind, 0.78);


            // Assign arrays
            oil_hx_prev = new double[xmax + 2, ymax + 2];
            oil_hy_prev = new double[xmax + 2, ymax + 2];

            oil_hx = new double[xmax + 2, ymax + 2];
            oil_hy = new double[xmax + 2, ymax + 2];

            oil_depth_prev = new double[xmax + 2, ymax + 2];
            oil_evap_fraction = new double[xmax + 2, ymax + 2];
            oil_depth = new double[xmax + 2, ymax + 2];
            oil_conc = new double[xmax + 2, ymax + 2];
            oil_concentration = new double[xmax + 2, ymax + 2];


        }
        bool oil_check_depth_threshold()
        {
            for (int x = Math.Min(oil_fromx, oil_tox); x <= Math.Max(oil_fromx, oil_tox); x++)
            {
                for (int y = Math.Min(oil_fromy, oil_toy); y <= Math.Max(oil_fromy, oil_toy); y++)
                {
                    if (water_depth[x, y] > oil_starth) return true;
                }
            }
            return false;
        }

        void scan_area()
        {
            double tempW = 0;// water_depth_erosion_threshold;
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                int inc = 1;

                for (int x = 1; x <= xmax; x++)
                {
                    // zero scan bit..
                    down_scan[y, x] = 0;
                    // and work out scanned area.
                    if (water_depth[x, y] > tempW
                          || water_depth[x - 1, y] > tempW
                          //|| water_depth[x - 1, y - 1] > tempW
                          //|| water_depth[x - 1, y + 1] > tempW
                          //|| water_depth[x + 1, y - 1] > tempW
                          //|| water_depth[x + 1, y + 1] > tempW
                          || water_depth[x, y - 1] > tempW
                          || water_depth[x + 1, y] > tempW
                          || water_depth[x, y + 1] > tempW)
                    {
                        down_scan[y, inc] = x;
                        inc++;
                    }
                }
            });



            Parallel.For(1, xmax + 1, options, delegate (int x)
            {

                int inc = 1;
                for (int y = 1; y <= ymax; y++)
                {
                    cross_scan[x, y] = 0;
                    if (water_depth[x, y] > tempW
                        || water_depth[x - 1, y] > tempW
                        || water_depth[x + 1, y] > tempW
                        || water_depth[x, y - 1] > tempW
                        || water_depth[x, y + 1] > tempW)
                    {
                        cross_scan[x, inc] = y;
                        inc++;
                    }
                    //discharge[x,y]=0;
                }

            });


        }

        private void zero_values()
        {
            int x, y, z, n;

            for (y = 0; y <= ymax; y++)
            {
                for (x = 0; x <= xmax; x++)
                {
                    Vel[x, y] = 0;
                    area[x, y] = 0;
                    elev[x, y] = 0;
                    angle_threshold[x, y] = 0;
                    bedrock[x, y] = -9999;
                    init_elevs[x, y] = elev[x, y];
                    water_depth[x, y] = 0;
                    index[x, y] = -9999;
                    inputpointsarray[x, y] = false;
                    veg[x, y, 0] = 0;// elevation
                    veg[x, y, 1] = 0; // density
                    veg[x, y, 2] = 0; // jw density
                    veg[x, y, 3] = 0; // height

                    edge[x, y] = 0;
                    edge2[x, y] = 0;


                    sand[x, y] = 0;

                    qx[x, y] = 0;
                    qy[x, y] = 0;

                    for (int T = 0; T <= tracers; T++) qxs[x, y, T] = 0;
                    for (int T = 0; T <= tracers; T++) qys[x, y, T] = 0;


                    for (n = 0; n <= 8; n++) vel_dir[x, y, n] = 0;

                    for (int T = 0; T <= tracers; T++) Vsusptot[x, y, T] = 0;

                    rfarea[x, y] = 1;

                    tracer_area[x, y] = 0;

                    grain_area[x, y] = 0;

                    if (SpatVarManningsCheckbox.Checked == true) spat_var_mannings[x, y] = mannings;

                    water_temp[x, y] = -9999; // TEMP_V1 - nodata sentinel; set to a real value
                    water_temp_prev[x, y] = -9999; // only for initially-wet cells, once water_depth is known (see load_data())

                }
            }

            for (x = 1; x < ((xmax * ymax) / LIMIT); x++)
            {

                for (y = 0; y <= G_MAX; y++)
                {
                    for (int T = 0; T <= tracers; T++) grain[x, y, T] = 0;
                }
                for (z = 0; z <= 9; z++)
                {
                    for (y = 0; y <= G_MAX - 2; y++)
                    {

                        for (int T = 0; T <= tracers; T++) strata[x, z, y, T] = 0;
                    }
                }
                catchment_input_x_coord[x] = 0;
                catchment_input_y_coord[x] = 0;
            }


            for (x = 1; x <= rfnum; x++)
            {
                j[x] = 0.000000001;
                jo[x] = 0.000000001;
                j_mean[x] = 0;
                old_j_mean[x] = 0;
                new_j_mean[x] = 0;
                M[x] = double.Parse(mvaluebox.Text);
            }
        }

        private void initialize_grain_strata()
        {
            int x, y, inc;
            grain_array_tot = 0;
            // initialize the grain and strata file if in tracer mode
            if (checkBox_tracer.Checked)
            {

                for (y = 1; y <= ymax; y++)
                {
                    for (x = 1; x <= xmax; x++)
                    {
                        if (elev[x, y] > -9999)
                        {
                            grain_array_tot++;
                            index[x, y] = grain_array_tot;

                            grain[grain_array_tot, 0, tracer_area[x, y]] = 0;
                            for (int i = 1; i <= G_MAX - 1; i++)
                            {
                                if (grain_area[x, y] == 0)
                                { grain[grain_array_tot, i, tracer_area[x, y]] = active * dprop[i]; }
                                else if (grain_area[x, y] == 1)
                                { grain[grain_array_tot, i, tracer_area[x, y]] = active * dprop_[i]; }
                            }
                            grain[grain_array_tot, G_MAX, tracer_area[x, y]] = 0;

                            for (int rr = 0; rr <= 9; rr++)
                            {
                                for (inc = 0; inc <= (G_MAX - 2); inc++)
                                {
                                    if (grain_area[x, y] == 0)
                                    { strata[grain_array_tot, rr, inc, tracer_area[x, y]] = active * dprop[inc + 1]; }
                                    else if (grain_area[x, y] == 1)
                                    { strata[grain_array_tot, rr, inc, tracer_area[x, y]] = active * dprop_[inc + 1]; }
                                }
                            }

                        }
                    }
                }
            }

        }

        void dune1(double time) // does dune things from top to bottom
        {
            int dune_recirculate = 0;
            int x, y, n, prob, counter1 = 1, ytemp, ytemp2, x2, y2;
            int t, checkup = int.Parse(upstream_check_box.Text);
            int flag = 1;

            double maxslabdepth = double.Parse(slab_depth_box.Text);
            int dep_probability = int.Parse(depo_prob_box.Text);
            int downstream_offset = int.Parse(offset_box.Text);
            double number_slabs_per_col = double.Parse(init_depth_box.Text);
            double angle = double.Parse(shadow_angle_box.Text);

            double fractiondune = double.Parse(fraction_dune.Text);
            double slabdepth = maxslabdepth;

            double factor = Math.Tan((angle * (3.141592654 / 180))) * (DX / dune_mult);
            Random xr = new Random();

            double[,] oldelev;
            oldelev = new double[xmax + 2, ymax + 2];

            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    // subtract eleev from sand so splitting into sand and elev for dune part...
                    oldelev[x, y] = elev[x, y];


                    double sandsum = 0;
                    double sand_diff = 0;
                    for (x2 = 0; x2 < dune_mult; x2++)
                    {
                        for (y2 = 0; y2 < dune_mult; y2++)
                        {
                            // this next line is needed to remove all sand from the far LH and RH cells
                            if (x == xmax) sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] = 0;
                            if (x == 1) sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] = 0;

                            if (sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] > 0)
                            {
                                sandsum += sand2[(x * dune_mult) - x2, (y * dune_mult) - y2];
                            }

                        }
                    }

                    // if water then transfer sand back to elev.
                    if (water_depth[x, y] >= water_depth_erosion_threshold) sand[x, y] = 0;


                    // then check and see if sand is less than sand2 - meaning there has been addition of dune sand to flucial sand
                    if (sand[x, y] < (sandsum / (dune_mult * dune_mult)))
                    {
                        sand_diff = (sandsum / (dune_mult * dune_mult)) - sand[x, y];


                        //if waterdepth >0 then sand > elev and grain (at a rate)
                        //then also reduce sand 2 by same amount transferred.



                        double tempsandvol = sand_diff;// amount to be removed from sand2...

                        // now has to reduce sand vol..
                        double sand2tot = 0;
                        int sand2num = 0;
                        for (x2 = 0; x2 < dune_mult; x2++)
                        {
                            for (y2 = 0; y2 < dune_mult; y2++)
                            {
                                if (sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] > 0)
                                {
                                    sand2tot += sand2[(x * dune_mult) - x2, (y * dune_mult) - y2];
                                    sand2num++;
                                }

                            }
                        }
                        for (x2 = 0; x2 < dune_mult; x2++)
                        {
                            for (y2 = 0; y2 < dune_mult; y2++)
                            {
                                if (sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] > 0)
                                {
                                    sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] -= (tempsandvol * dune_mult * dune_mult) *
                                        (sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] / sand2tot);

                                }

                            }
                        }
                    }

                    elev[x, y] -= sand[x, y];

                    // check to add to sand from grain (if no water)
                    // if so then update sand and sand2 (and elev if subtracting from grain and
                    // adding to sand

                    if (water_depth[x, y] < water_depth_erosion_threshold)
                    {
                        if (elev[x, y] - init_elevs[x, y] > 0)
                        {
                            double tempslabdepth = elev[x, y] - init_elevs[x, y];
                            //if (tempslabdepth < slabdepth) tempslabdepth = 0;
                            if (tempslabdepth > slabdepth) tempslabdepth = slabdepth;
                            sand[x, y] += tempslabdepth;
                            elev[x, y] -= tempslabdepth;

                            // now also update sand2 values... averaged across cell.
                            for (x2 = 0; x2 < dune_mult; x2++)
                            {
                                for (y2 = 0; y2 < dune_mult; y2++)
                                {
                                    sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] += tempslabdepth;
                                }
                            }

                        }

                    }

                    //update elev2 from elev
                    for (x2 = 0; x2 < dune_mult; x2++)
                    {
                        for (y2 = 0; y2 < dune_mult; y2++)
                        {
                            elev2[(x * dune_mult) - x2, (y * dune_mult) - y2] = elev[x, y];
                        }
                    }

                }
            }

            /////////////////////////////////////////////////////////////
            // adding sand part
            /////////////////////////////////////////////////////////////

            //if (Math.IEEERemainder((int)(cycle / 60000), 2) == 0)
            //if (Math.IEEERemainder((int)(cycle), 2) == 0)

            for (x = 50; x < (xmax) * dune_mult * fractiondune; x++)
            {
                //if(Math.IEEERemainder(Math.Abs(x/50)-1,3)==0)sand2[x, 1] = initial_sand_depth;
                sand2[xr.Next(50, (xmax * dune_mult)), 1] += number_slabs_per_col;

            }

            /////////////////////////////////////////////////////////////
            // end of adding sand code ///
            /////////////////////////////////////////////////////////////


            // check everywhere for sand landslides
            for (x = 1; x <= xmax * dune_mult; x++)
            {
                for (y = 1; y <= ymax * dune_mult; y++)
                {
                    slide_4(x, y);
                }
            }


            for (n = 1; n <= fractiondune * (xmax * dune_mult) * (ymax * dune_mult); n++)
            {

                // creating random x and y co-ords to check...
                x = xr.Next(1, (xmax * dune_mult) + 1);
                y = xr.Next(1, (ymax * dune_mult) + 1);
                prob = 100;
                counter1 = 0;
                flag = 1;

                //if (sand2[x, y] >= slabdepth)
                if (sand2[x, y] >= slabdepth && water_depth[Math.Abs(x / dune_mult), Math.Abs(y / dune_mult)] < water_depth_erosion_threshold)
                {
                    // to see if should be entrained or not...
                    for (t = 1; t <= checkup; t++)
                    {
                        ytemp = y - t;
                        if (dune_recirculate == 1 && ytemp < 1) ytemp = (ymax * dune_mult) + ytemp;
                        if (dune_recirculate == 0 && ytemp < 1) ytemp = 1;
                        if (((sand2[x, ytemp] + elev2[x, ytemp]) - (sand2[x, y] + elev2[x, y])) > (factor * (t)))
                        {
                            flag = 0;
                            t = checkup;
                        }

                    }


                    // now having decided it can be eroded, now see if it can be moved
                    if (flag == 1)
                    {
                        // while random number greater than prob then move on one if poss..
                        while (prob > dep_probability)
                        {
                            counter1++; // shift down one cell...
                            prob = xr.Next(1, 100); // new random number

                            //// try moving left and right??
                            //if (xr.Next(1, 100) > 70) x++;
                            //if (xr.Next(1, 100) > 70) x--;
                            //if (x < 1) x = (xmax * dune_mult);
                            //if (x > (xmax * dune_mult)) x = 1;


                            // now adding in the downsrtream offset if needed done by ensuring prob is 100
                            if (counter1 < downstream_offset) prob = 100; // break;

                            // change comments on below lines if you want water to stop sand movement or not
                            //if (water_depth[Math.Abs(x/dune_mult),Math.Abs((y)/dune_mult)] >= water_depth_erosion_threshold) prob = 0;
                            if (water_depth[Math.Abs(x / dune_mult), Math.Abs((y + Math.Abs(counter1 / dune_mult)) / dune_mult)] >= water_depth_erosion_threshold) prob = 0;

                            // seeing if in shadow or not...
                            for (t = 1; t < checkup; t++)
                            {
                                ytemp = (y + counter1) - t;
                                ytemp2 = (y + counter1);
                                if (dune_recirculate == 0 && ytemp < 1) ytemp = 1;
                                if (dune_recirculate == 1 && ytemp < 1) ytemp = (ymax * dune_mult) + ytemp;
                                if (dune_recirculate == 1)
                                {
                                    if (ytemp > (ymax * dune_mult)) ytemp = 0 + (ytemp - (ymax * dune_mult));
                                    if (ytemp2 > (ymax * dune_mult)) ytemp2 = 0 + (ytemp2 - (ymax * dune_mult));
                                }
                                if (((sand2[x, ytemp] + elev2[x, ytemp]) - (sand2[x, ytemp2] + elev2[x, ytemp2])) > (factor * t))
                                {
                                    t = checkup;
                                    prob = 0;
                                }

                            }
                            //if (elev[x, ytemp - 1] - elev[x, y] > factor) break;
                        }

                        double tempmax = sand2[x, y];
                        if (tempmax < 0) tempmax = 0;



                        // now erode sand if there is enough there in the sand layer
                        if (slabdepth < tempmax) tempmax = slabdepth;
                        if (tempmax < 0) tempmax = 0;
                        sand2[x, y] -= tempmax;
                        // do landslides for just that cell and ones around.
                        slide_4(x, y);

                        ytemp = y + counter1;

                        if (dune_recirculate == 1)
                        {
                            if (ytemp > (ymax * dune_mult))
                            {
                                ytemp = ytemp - (ymax * dune_mult);
                                sand_out += (tempmax * (DX / dune_mult) * (DX / dune_mult));
                            }
                        }

                        // now deposit sand

                        if (ytemp <= ymax * dune_mult)
                        {
                            sand2[x, ytemp] += tempmax;

                            // do landslides for just that cell and ones around.
                            slide_4(x, ytemp);
                        }
                        if (ytemp > (ymax * dune_mult)) sand_out += (tempmax * (DX / dune_mult) * (DX / dune_mult));

                    }
                }

            }




            // now do landslides everywhere 5 times... just to make sure..
            for (t = 1; t <= 5; t++)
            {
                for (x = 1; x <= xmax * dune_mult; x++)
                {
                    for (y = 1; y <= ymax * dune_mult; y++)
                    {
                        if (sand2[x, y] > 0) slide_4(x, y);
                    }
                }
            }

            // sand = mean sand2

            for (x = 1; x <= (xmax); x++)
            {
                for (y = 1; y <= (ymax); y++)
                {

                    double sandsum = 0;
                    for (x2 = 0; x2 < dune_mult; x2++)
                    {
                        for (y2 = 0; y2 < dune_mult; y2++)
                        {
                            // this next line is needed to remove all sand from the far LH and RH cells
                            if (x == xmax) sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] = 0;
                            if (x == 1) sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] = 0;

                            if (sand2[(x * dune_mult) - x2, (y * dune_mult) - y2] > 0)
                            {
                                sandsum += sand2[(x * dune_mult) - x2, (y * dune_mult) - y2];
                            }

                        }
                    }


                    sand[x, y] = sandsum / (dune_mult * dune_mult);

                    double newelev = elev[x, y] + sand[x, y];
                    elev[x, y] = oldelev[x, y];
                    elev_diff[x, y] = oldelev[x, y] - newelev;

                }

            }



        }

        void calc_hillshade() // <JOE 20051605- begin>
        {
            //Local variables
            int x, y;

            double slopemax;
            double slope;
            int slopetot;
            double local_Illumination;

            // Initialize Hillshade Paramaters
            double azimuth = 315 * (3.141592654 / 180); // Default of 315 degrees converted to radians
            double altitude = 45 * (3.141592654 / 180); // Default of 45 degrees converted to radians



            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    if (elev[x, y] != -9999)
                    {
                        slopemax = 0.0;
                        slope = 0.0;
                        slopetot = 0;

                        // Do slope analysis and Aspect Calculation first
                        if (elev[x, y] > elev[x, y - 1] && elev[x, y - 1] != -9999) // North 0
                        {
                            slope = Math.Pow((elev[x, y] - elev[x, y - 1]) / root, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 0 * (3.141592654 / 180);
                            }

                        }
                        if (elev[x, y] > elev[x + 1, y - 1] && elev[x + 1, y - 1] != -9999) // Northeast 45
                        {
                            slope = Math.Pow((elev[x, y] - elev[x + 1, y - 1]) / DX, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 45 * (3.141592654 / 180);
                            }
                        }
                        if (elev[x, y] > elev[x + 1, y] && elev[x + 1, y] != -9999) // East 90
                        {
                            slope = Math.Pow((elev[x, y] - elev[x + 1, y]) / root, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 90 * (3.141592654 / 180);
                            }
                        }
                        if (elev[x, y] > elev[x + 1, y + 1] && elev[x + 1, y + 1] != -9999) // SouthEast 135
                        {
                            slope = Math.Pow((elev[x, y] - elev[x + 1, y + 1]) / root, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 135 * (3.141592654 / 180);
                            }

                        }
                        if (elev[x, y] > elev[x, y + 1] && elev[x, y + 1] != -9999) // South 180
                        {
                            slope = Math.Pow((elev[x, y] - elev[x, y + 1]) / DX, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 180 * (3.141592654 / 180);
                            }
                        }
                        if (elev[x, y] > elev[x - 1, y + 1] && elev[x - 1, y + 1] != -9999) // SouthWest 225
                        {
                            slope = Math.Pow((elev[x, y] - elev[x - 1, y + 1]) / root, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 225 * (3.141592654 / 180);
                            }
                        }
                        if (elev[x, y] > elev[x - 1, y] && elev[x - 1, y] != -9999) // West 270
                        {
                            slope = Math.Pow((elev[x, y] - elev[x - 1, y]) / root, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 270;
                            }
                        }
                        if (elev[x, y] > elev[x - 1, y - 1] && elev[x - 1, y - 1] != -9999) // Northwest 315
                        {
                            slope = Math.Pow((elev[x, y] - elev[x - 1, y - 1]) / DX, 1);
                            if (slope > slopemax)
                            {
                                slopemax = slope;
                                slopetot++;
                                aspect[x, y] = 315 * (3.141592654 / 180);
                            }
                        }

                        if (slope > 0) slopeAnalysis[x, y] = slopemax;// Tom's: (slope/slopetot); ?

                        // Convert slope to radians
                        slopeAnalysis[x, y] = System.Math.Atan(slopeAnalysis[x, y]);


                        // Do Hillshade Calculation
                        local_Illumination = 255 * ((System.Math.Cos(azimuth)
                                                     * System.Math.Sin(slopeAnalysis[x, y])
                                                     * System.Math.Cos(aspect[x, y] - azimuth))
                                                   + (System.Math.Sin(altitude)
                                                     * System.Math.Cos(slopeAnalysis[x, y])));

                        hillshade[x, y] = System.Math.Abs(local_Illumination);
                    }
                }
            }

        }       // End calc_hillshade() <JOE 20051605- end>

        void Color_HSVtoRGB()   // <JOE 20051605>
        {
            // Convert HSV to RGB.
            // Made this a seperate function as it is called multiple times in drawwater().

            if (sat == 0)
            {
                // If sat is 0, all colors are the same.
                // This is some flavor of gray.
                red = val;
                green = val;
                blue = val;
            }
            else
            {
                double pFactor;
                double qFactor;
                double tFactor;

                double fractionalSector;
                int sectorNumber;
                double sectorPos;

                // The color wheel consists of six 60 degree sectors.
                // Figure out which sector you are in.
                sectorPos = hue / 60;
                sectorNumber = (int)(Math.Floor(sectorPos));

                // get the fractional part of the sector.
                // That is, how many degrees into the sector are you?
                fractionalSector = sectorPos - sectorNumber;

                // Calculate values for the three axes
                // of the color.
                pFactor = val * (1 - sat);
                qFactor = val * (1 - (sat * fractionalSector));
                tFactor = val * (1 - (sat * (1 - fractionalSector)));

                // Assign the fractional colors to r, g, and b based on the sector the angle is in.
                switch (sectorNumber)
                {
                    case 0:
                        red = val;
                        green = tFactor;
                        blue = pFactor;
                        break;
                    case 1:
                        red = qFactor;
                        green = val;
                        blue = pFactor;
                        break;
                    case 2:
                        red = pFactor;
                        green = val;
                        blue = tFactor;
                        break;
                    case 3:
                        red = pFactor;
                        green = qFactor;
                        blue = val;
                        break;
                    case 4:
                        red = tFactor;
                        green = pFactor;
                        blue = val;
                        break;
                    case 5:
                        red = val;
                        green = pFactor;
                        blue = qFactor;
                        break;
                }
            }
        }

        void drawwater(System.Drawing.Graphics graphics)// <JMW 20041018>
        {
            if (!simLoadState) return; // MDW: if data are not loaded, do nothing (fixes an exception which happens when selecting an item from the graphics menu before data are loaded)

            Graphics objGraphics;
            objGraphics = Graphics.FromImage(m_objDrawingSurface);
            objGraphics.Clear(SystemColors.Control);

            int x, y, z, tot;
            int redcol = 0, greencol = 0, bluecol = 0, alphacol = 255;
            int t = 0;
            double tot_max, tomsedi = 0;


            // load background image....
            //try
            //{
            //    Image tom1 = Image.FromFile(@"img.png");
            //    objGraphics.DrawImage(tom1, 0, 0, xmax * graphics_scale, ymax * graphics_scale);
            //}
            //catch
            //{
            //}


            // Set Graphics Display Size
            if (xmax <= 0) xmax = 1;

            //set scaling of graphics - so X bmp pixels to every model pixel.
            t = graphics_scale;

            // These loop through the entire grid
            // DEM <JOE 20050905>
            if (menuItem30.Checked == true) // DEM
            {
                double zDEM;
                double zCalc, zMin = 100000.0, zMax = -9990.0, zRange, hsMin = 0, hsMax = 255, hsRange, hs;
                double valMin = 0.0;
                double valMax = 1.0;



                calc_hillshade();       // Call up routine

                // First, find max, min and range of DEM and Hillshade
                for (x = 1; x <= xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        zCalc = elev[x, y];
                        if (zCalc != -9999)
                        {
                            if (zCalc < zMin) zMin = zCalc;
                            if (zCalc > zMax) zMax = zCalc;
                            hs = hillshade[x, y];
                            if (hs < hsMin) hsMin = hs;
                            if (hs > hsMax) hsMax = hs;
                        }
                    }
                }
                zRange = zMax - zMin;
                hsRange = hsMax - hsMin;

                for (x = 1; x <= xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        if (elev[x, y] > -9999.0)
                        {
                            // HILLSHADE: Draw first underneath

                            // set gray scale intensity
                            hue = 360.0;    // hue doesn't matter for gray shade
                            sat = 0.0;      // ensures gray shade
                            valMin = 0.0;
                            valMax = 1.0;
                            val = ((hillshade[x, y] / 255) * (valMax - valMin)) + valMin; // uses maximum contrast
                                                                                          //							val = (((hillshade[x,y] - hsMin)/(zRange)) * (valMax - valMin)) + valMin; // lower contrast

                            Color_HSVtoRGB();   // Call up color conversion
                            redcol = System.Convert.ToInt32(red * 255);
                            greencol = System.Convert.ToInt32(green * 255);
                            bluecol = System.Convert.ToInt32(blue * 255);
                            alphacol = 255;

                            SolidBrush brush2 = new SolidBrush(Color.FromArgb(alphacol, redcol, greencol, bluecol));
                            objGraphics.FillRectangle(brush2, (x - 1) * t, (y - 1) * t, t, t);


                            // DEM
                            zDEM = (elev[x, y]);
                            // Sets hue based on desired color range (in decimal degrees; max 360)
                            double hueMin = 30.0;
                            double hueMax = 85.0;
                            //							hue = (((zDEM - zMin)/(zRange)) * (hueMax - hueMin)) + hueMin; // Forward
                            hue = hueMax - (((zDEM - zMin) / (zRange)) * (hueMax - hueMin)); // Reverse

                            // Set saturation based on desired range
                            double satMin = 0.50;
                            double satMax = 0.95;
                            sat = (((zDEM - zMin) / (zRange)) * (satMax - satMin)) + satMin;
                            //							sat = 0; // Use for grey-scale DEM only!

                            // Set value based on desired range
                            valMin = 0.40;
                            valMax = 0.80;
                            val = (((zDEM - zMin) / (zRange)) * (valMax - valMin)) + valMin;
                            //							val = valMax - (((zDEM - zMin)/(zRange)) * (valMax - valMin));

                            Color_HSVtoRGB();   // Call up color conversion
                            redcol = System.Convert.ToInt32(red * 255);
                            greencol = System.Convert.ToInt32(green * 255);
                            bluecol = System.Convert.ToInt32(blue * 255);
                            if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                            alphacol = 125;

                            SolidBrush brush = new SolidBrush(Color.FromArgb(alphacol, redcol, greencol, bluecol));
                            objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                        }   // Close of Entire Grid Mask
                    }       // Close of Column Loop
                }           // Close of Row Loop

            }               // Close of DEM check box (menuitem34)


            // Find Ranges for those in Active Area
            // Water Depth Range:
            double wdCalc, wdMin = 100000.0, wdMax = -10.0, wdRange;

            // Find Water Depth Ranges
            for (x = 1; x < xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    wdCalc = water_depth[x, y];
                    if (wdCalc > 0)
                    {
                        if (wdCalc < wdMin) wdMin = wdCalc;
                        if (wdCalc > wdMax) wdMax = wdCalc;
                    }
                }
            }
            wdRange = wdMax - wdMin;

            // OIL_V1_MDW
            // Find range of oil depths if needed
            double oilCalc, oilMin = 1000.0, oilMax = -10, oilRange = 1;
            if (isOilSimulation && menuItemOilVisualisation.Checked == true)
            {
                for (x = 1; x < xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        oilCalc = oil_depth[x, y];


                        if (oilCalc > 0)
                        {
                            if (oilCalc < oilMin) oilMin = oilCalc;
                            if (oilCalc > oilMax) oilMax = oilCalc;


                        }
                    }
                }
                if (oilMax > 0) oilRange = oilMax - oilMin;
            }

            // OIL_V1_PDF
            // Find range of oil concentration if needed
            double oilConc, oilCMin = 0.00000001, oilCMax = 0.0000001, oilCRange = 0.01;
            if (isOilSimulation && menuItemOilconcVisualisation.Checked == true)
            {
                for (x = 1; x < xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        oilConc = oil_concentration[x, y];


                        if (oilConc > 0)
                        {
                            if (oilConc < oilCMin) oilCMin = oilConc;
                            if (oilConc > oilCMax) oilCMax = oilConc;


                        }
                    }
                }
                if (oilCMax > 0) oilCRange = oilCMax - oilCMin;
            }

            // TEMP_V1
            // Find range of water temperature if needed (excludes nodata / dry cells)
            double tempCalc, tempMin = 1000.0, tempMax = -1000.0, tempRangeVal = 1.0;
            if (isSimulateTemperature && menuItemWaterTempVisualisation.Checked == true)
            {
                for (x = 1; x <= xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        if (water_depth[x, y] > water_depth_erosion_threshold && water_temp[x, y] != -9999)
                        {
                            tempCalc = water_temp[x, y];
                            if (tempCalc < tempMin) tempMin = tempCalc;
                            if (tempCalc > tempMax) tempMax = tempCalc;
                        }
                    }
                }
                if (tempMax > tempMin) tempRangeVal = tempMax - tempMin;
            }

            // All these loop through just the 'Active Area'
            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    if (1 > 0) // Index masks out so only 'active cells' shown	was if(index[x,y]>-9999)
                    {
                        // Water Depth
                        if (menuItem3.Checked == true && water_depth[x, y] > water_depth_erosion_threshold)//MIN_Q)//||discharge[x,y]>0)
                        {

                            z = 0;
                            if (comboBox1.Text == "water depth")
                            {
                                z = (int)(water_depth[x, y] * (256 / wdRange));
                            }
                            else
                            {
                                z = (int)(water_depth[x, y] * 128);
                            }

                            if (z < 0) z = 0;
                            if (z > 254) z = 254;

                            greencol = 255 - z;
                            redcol = z;
                            bluecol = 255;
                            if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                            if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                            alphacol = 255;

                            SolidBrush brush = new SolidBrush(Color.FromArgb(alphacol, redcol, greencol, bluecol));
                            objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                        }

                        // DoD Erosion/ Depostion
                        if (menuItem4.Checked == true)
                        {
                            tomsedi += (init_elevs[x, y] - elev[x, y]);
                            tomsedi -= (Vsusptot[x, y, 0]);
                            if (init_elevs[x, y] - elev[x, y] >= 0.001) //eroding
                            {
                                if (comboBox1.Text == "erosion/dep")
                                {
                                    z = (int)((init_elevs[x, y] - elev[x, y]) * 256 * contrastMultiplier);
                                }
                                else
                                {
                                    z = (int)((init_elevs[x, y] - elev[x, y]) * 64);
                                }
                                if (z < 0) z = 0;
                                if (z > 254) z = 254;
                                greencol = 255 - z;
                                redcol = z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;
                                // red green blue

                                SolidBrush brush = new SolidBrush(Color.FromArgb(255, greencol, greencol));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                            }

                            if (init_elevs[x, y] - elev[x, y] <= -0.001) //depositing
                            {
                                z = (int)((elev[x, y] - init_elevs[x, y]) * 64);
                                if (z < 0) z = 0;
                                if (z > 254) z = 254;
                                greencol = z;
                                redcol = 255 - z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, 255, redcol));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                            }

                            //						if(init_elevs[x,y]-elev[x,y]>0.0001&&init_elevs[x,y]-elev[x,y]<0.1)graphics.DrawRectangle(Pens.Yellow,x*t,y*t,t,t);
                            //						if(init_elevs[x,y]-elev[x,y]>0.1&&init_elevs[x,y]-elev[x,y]<0.2)graphics.DrawRectangle(Pens.Orange,x*t,y*t,t,t);
                            //						if(init_elevs[x,y]-elev[x,y]>0.2&&init_elevs[x,y]-elev[x,y]<10)graphics.DrawRectangle(Pens.Red,x*t,y*t,t,t);
                            //						if(init_elevs[x,y]-elev[x,y]<-0.0001&&init_elevs[x,y]-elev[x,y]>-0.1)graphics.DrawRectangle(Pens.LightGreen,x*t,y*t,t,t);
                            //						if(init_elevs[x,y]-elev[x,y]<-0.1&&init_elevs[x,y]-elev[x,y]>-0.2)graphics.DrawRectangle(Pens.Green,x*t,y*t,t,t);
                            //						if(init_elevs[x,y]-elev[x,y]<-0.2&&init_elevs[x,y]-elev[x,y]>-10)graphics.DrawRectangle(Pens.DarkGreen,x*t,y*t,t,t);


                        }

                        // Water Source Tracers - MDW 17-03-2016
                        if (menuItem10.Checked == true && water_depth[x, y] > water_depth_erosion_threshold)
                        {
                            int srcR, srcG, srcB;
                            redcol = 0; greencol = 0; bluecol = 0;
                            //z = 0;
                            // MDW_V2 modified to enable more complete labelling, and fixed an issue where selecting no sources could lead to an exception overload
                            try { srcR = int.Parse(comboBox2.Text.Substring(0, 1)) - 1; } // adjust for zero-index
                            catch { srcR = -1; } //R : exception should now never be triggered
                            try { srcG = int.Parse(comboBox3.Text.Substring(0, 1)) - 1; }
                            catch { srcG = -1; } //G
                            try { srcB = int.Parse(comboBox4.Text.Substring(0, 1)) - 1; }
                            catch { srcB = -1; } //B

                            if (comboBox1.Text == "water source tracer")
                            {   // MDW_V2 modified comparisons since "no source" (e.g. srcR == 0) is now a valid selection
                                // reordered logic for inclusion of solutes
                                if (checkBox12.Checked == true) // rainfall zone sources
                                {
                                    if (srcR != -1) redcol = (int)(Math.Pow(watertracerRainZone[x, y, srcR], enhanceValue) * 256);   // MDW_V2 updated reference
                                    if (srcG != -1) greencol = (int)(Math.Pow(watertracerRainZone[x, y, srcG], enhanceValue) * 256); //
                                    if (srcB != -1) bluecol = (int)(Math.Pow(watertracerRainZone[x, y, srcB], enhanceValue) * 256);  //
                                }
                                else if (checkBoxSoluteVis.Checked == true) // solutes
                                {
                                    if (srcR != -1)
                                    {
                                        if (soluteMax[srcR] == 0) { redcol = 0; }
                                        else { redcol = (int)(Math.Pow(solutetracer[x, y, srcR] / soluteMax[srcR], enhanceValue) * 256); }  // scale 0 to 1
                                        if (redcol > 256) { redcol = 256; }
                                    }

                                    if (srcG != -1)
                                    {
                                        if (soluteMax[srcG] == 0) { greencol = 0; }
                                        else { greencol = (int)(Math.Pow(solutetracer[x, y, srcG] / soluteMax[srcG], enhanceValue) * 256); }
                                        if (greencol > 256) { greencol = 256; }

                                    }
                                    if (srcB != -1)
                                    {
                                        if (soluteMax[srcB] == 0) { bluecol = 0; }
                                        else { bluecol = (int)(Math.Pow(solutetracer[x, y, srcB] / soluteMax[srcB], enhanceValue) * 256); }
                                        if (bluecol > 256) { bluecol = 256; }
                                    }
                                }
                                else  // normal water sources
                                {
                                    if (srcR != -1) redcol = (int)(Math.Pow(watertracer[x, y, srcR], enhanceValue) * 256);
                                    if (srcG != -1) greencol = (int)(Math.Pow(watertracer[x, y, srcG], enhanceValue) * 256);
                                    if (srcB != -1) bluecol = (int)(Math.Pow(watertracer[x, y, srcB], enhanceValue) * 256);
                                }
                            }
                            else // include depth shading
                            {
                                // MDW_V2 reordered logic to include solutes
                                if (checkBox12.Checked == true) // rainfall zone sources
                                {
                                    if (srcR != -1) redcol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(watertracerRainZone[x, y, srcR], enhanceValue)));   // MDW_V2 updated reference
                                    else redcol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                    if (srcG != -1) greencol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(watertracerRainZone[x, y, srcG], enhanceValue))); //
                                    else greencol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                    if (srcB != -1) bluecol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(watertracerRainZone[x, y, srcB], enhanceValue)));  //
                                    else bluecol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                }
                                else if (checkBoxSoluteVis.Checked == true) // solutes
                                {
                                    // logic here checks if:
                                    // soluteMax is 0: will only be the case if the user enters zero for all values for that solute
                                    // solutetracer > soluteMax: will only be the case due to evaporation, if active
                                    if (srcR != -1)
                                    {
                                        if (soluteMax[srcR] == 0) { redcol = (int)((1 - (water_depth[x, y] / wdRange)) * 128); }
                                        else if (solutetracer[x, y, srcR] > soluteMax[srcR]) { redcol = (int)((1 - (water_depth[x, y] / wdRange)) * 128) + 128; }
                                        else { redcol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(solutetracer[x, y, srcR] / soluteMax[srcR], enhanceValue))); }  // Scale solute 0 to 1
                                    }
                                    else redcol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                    if (srcG != -1)
                                    {
                                        if (soluteMax[srcG] == 0) { greencol = (int)((1 - (water_depth[x, y] / wdRange)) * 128); }
                                        else if (solutetracer[x, y, srcG] > soluteMax[srcG]) { greencol = (int)((1 - (water_depth[x, y] / wdRange)) * 128) + 128; }
                                        else { greencol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(solutetracer[x, y, srcG] / soluteMax[srcG], enhanceValue))); }//
                                    }
                                    else greencol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                    if (srcB != -1)
                                    {
                                        if (soluteMax[srcB] == 0) { bluecol = (int)((1 - (water_depth[x, y] / wdRange)) * 128); }
                                        else if (solutetracer[x, y, srcB] > soluteMax[srcB]) { bluecol = (int)((1 - (water_depth[x, y] / wdRange)) * 128) + 128; }
                                        else { bluecol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(solutetracer[x, y, srcB] / soluteMax[srcB], enhanceValue))); } //
                                    }
                                    else bluecol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);

                                }
                                else // normal water sources
                                {
                                    if (srcR != -1) redcol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(watertracer[x, y, srcR], enhanceValue)));
                                    else redcol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                    if (srcG != -1) greencol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(watertracer[x, y, srcG], enhanceValue)));
                                    else greencol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                    if (srcB != -1) bluecol = (int)(((1 - (water_depth[x, y] / wdRange)) * 128) + (128 * Math.Pow(watertracer[x, y, srcB], enhanceValue)));
                                    else bluecol = (int)((1 - (water_depth[x, y] / wdRange)) * 128);
                                }

                            }

                            //if (redcol < 0) redcol = 0;
                            //if (redcol > 254) redcol = 254;
                            //if (greencol < 0) greencol = 0;
                            //if (greencol > 254) greencol = 254;
                            //if (bluecol < 0) bluecol = 0;
                            //if (bluecol > 254) bluecol = 254;

                            //greencol = 255 - z;
                            //redcol = z;
                            //bluecol = 255;

                            if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                            if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                            alphacol = 255;

                            SolidBrush brush = new SolidBrush(Color.FromArgb(alphacol, redcol, greencol, bluecol));
                            objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                        }


                        if (menuItem5.Checked == true)
                        {
                            //if(water_depth[x,y]>water_depth_erosion_threshold)//if(index[x,y]!=-9999)
                            {
                                z = (int)(veg[x, y, 1] * 256);

                                //z = rfarea[x, y] * 120;
                                //z = (int)(rfarea[x, y] * 12.6);

                                //z = (int)(Vsusptot[x, y] * 20050);
                                //z = (int)(discharge[x,y] * 50.6);
                                //z = (int)(edge2[x,y] * 1000);
                                //z = (int)(Math.Abs(mean_bed_slope (x, y)) * 50 * 256);

                                if (z < 0) z = 0;
                                if (z > 254) z = 254;
                                greencol = z;
                                redcol = 255 - z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, 255, redcol));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                            }
                        }


                        // now to draw second teir


                        if (menuItem7.Checked == true && edge[x, y] > -9999)
                        {

                            if (edge[x, y] >= 0) //Outside
                            {
                                z = 0;
                                z = (int)((edge[x, y]) * 20000);

                                if (z < 0) z = 0;
                                //							z=(int)((2.131*Math.Pow(edge[x,y],-1.0794))*DX);
                                //							z=254-z;
                                if (z > 254) z = 254;
                                if (z < 0) z = 0;
                                greencol = 255 - z;
                                redcol = z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;
                                // red green blue

                                SolidBrush brush = new SolidBrush(Color.FromArgb(255, greencol, greencol));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                            }

                            if (edge[x, y] < 0) //inside
                            {
                                z = (int)((0 - edge[x, y]) * 50);
                                if (z < 0) z = 0;
                                if (z > 254) z = 254;
                                //							z=(int)((2.131*Math.Pow(edge[x,y],-1.0794))*DX);
                                //							z=254+z;
                                //							if(z>254)z=254;
                                //							if(z<0)z=0;
                                greencol = z;
                                redcol = 255 - z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, 255, redcol));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                            }


                        }

                        if (menuItem8.Checked == true)


                        {

                            if (water_depth[x, y] > water_depth_erosion_threshold)
                            {
                                if (comboBox1.Text == "Bed sheer stress")
                                {
                                    z = (int)(contrastMultiplier * 5 * Tau[x, y]);
                                }
                                else
                                {
                                    z = (int)(5 * Tau[x, y]);
                                    //z = (int)(Tau[x, y] * (4));
                                }

                                if (z < 0) z = 0;
                                if (z > 254) z = 254;
                                //if(z>100)z=254;
                                greencol = 255 - z;
                                redcol = z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, greencol, 255));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                            }

                        }

                        // D50
                        if (menuItem9.Checked == true)
                        {
                            if (index[x, y] != -9999)
                            {
                                if (comboBox1.Text == "grainsize")
                                {
                                    tot_max = d50(index[x, y]) * 2000 * contrastMultiplier;
                                }
                                else
                                {
                                    tot_max = d50(index[x, y]) * 2000;
                                }
                                //Console.WriteLine((Convert.ToString(tot_max)));

                                tot = (int)tot_max;
                                if (tot > 255) tot = 255;
                                if (tot < 0) tot = 0;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(255 - (tot * 1), 255 - (tot * 1), 255 - (tot * 1)));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                            }
                        }



                        // area drained
                        if (menuItem26.Checked == true)
                        {
                            if (area[x, y] != -9999)
                            {
                                z = (int)area[x, y];


                                if (z < 0) z = 0;
                                if (z > 254) z = 254;

                                greencol = 255 - z;
                                redcol = z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, greencol, 255));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                            }
                        }

                        // Suspended Sediment Concentration
                        if (menuItem27.Checked == true)
                        {
                            if (index[x, y] != -9999)
                            {
                                if (Vsusptot[x, y, 0] > 0.0)
                                {
                                    if (comboBox1.Text == "susp conc")
                                    {
                                        z = (int)(Vsusptot[x, y, 0] * 25600 * contrastMultiplier);
                                    }
                                    else
                                    {
                                        z = (int)(Vsusptot[x, y, 0] * 25600);
                                    }
                                    if (z < 0) z = 0;
                                    if (z > 254) z = 254;

                                    greencol = 255 - z;
                                    redcol = z;
                                    if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                    if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                    SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, greencol, 255));
                                    objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                                }
                            }
                        }

                        // Soil depth
                        if (menuItem28.Checked == true)
                        {
                            if (elev[x, y] > -9999)
                            {

                                if (comboBox1.Text == "soildepth")
                                {
                                    z = (int)((elev[x, y] - bedrock[x, y]) * 2500 * contrastMultiplier);
                                }
                                else
                                {
                                    z = (int)((elev[x, y] - bedrock[x, y]) * 2500);
                                }
                                if (z < 0) z = 0;
                                if (z > 254) z = 254;
                                greencol = 255 - z;
                                redcol = z;
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, greencol, 255));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);

                            }
                        }

                        // Flow Velocity
                        if (menuItem31.Checked == true) // <JOE 20050605>
                        {
                            if (index[x, y] != -9999)
                            {
                                if (water_depth[x, y] > water_depth_erosion_threshold)
                                {
                                    z = 0;
                                    if (comboBox1.Text == "flow velocity")
                                    {
                                        z = (int)((Vel[x, y]) * 125 * contrastMultiplier);
                                    }
                                    else
                                    {
                                        z = (int)((Vel[x, y]) * 125);
                                    }
                                    //if(yyy>0.25)z=254;
                                    if (z < 0) z = 0;
                                    if (z > 254) z = 254;
                                    greencol = 255 - z;
                                    redcol = z;
                                    if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                    if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                    SolidBrush brush = new SolidBrush(Color.FromArgb(redcol, greencol, 255));
                                    objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                                }
                            }
                        }

                        // Water Temperature - TEMP_V1
                        if (menuItemWaterTempVisualisation.Checked == true && isSimulateTemperature == true)
                        {
                            if (water_depth[x, y] > water_depth_erosion_threshold && water_temp[x, y] != -9999)
                            {
                                double tNorm = (water_temp[x, y] - tempMin) / tempRangeVal;
                                if (tNorm < 0) tNorm = 0;
                                if (tNorm > 1) tNorm = 1;

                                // Diverging blue (cold) - white (mid) - red (hot) colour scale
                                if (tNorm < 0.5)
                                {
                                    double f = tNorm / 0.5;
                                    redcol = (int)(f * 255);
                                    greencol = (int)(f * 255);
                                    bluecol = 255;
                                }
                                else
                                {
                                    double f = (tNorm - 0.5) / 0.5;
                                    redcol = 255;
                                    greencol = (int)(255 * (1 - f));
                                    bluecol = (int)(255 * (1 - f));
                                }
                                if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                SolidBrush brush = new SolidBrush(Color.FromArgb(255, redcol, greencol, bluecol));
                                objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                            }
                        }

                        // Oil spill - OIL_V1_MDW
                        if (menuItemOilVisualisation.Checked == true && isOilSimulation == true)
                        {
                            if (index[x, y] != -9999)
                            {
                                if (oil_depth[x, y] > 0.00001 && water_depth[x, y] > water_depth_erosion_threshold)
                                {
                                    z = (int)(oil_depth[x, y] * (128 / oilRange)); // using lower half of grey range for darker cells
                                    if (comboBox1.Text == "oil spill") // display only the oil spill for this cell (opaque cell)
                                    {
                                        alphacol = 255;
                                    }
                                    else // display on top of whatever else is selected (semi-transparent cell)
                                    {
                                        alphacol = 128;
                                    }
                                    if (z < 0) z = 0;
                                    if (z > 128) z = 128;
                                    greencol = redcol = bluecol = 128 - z; // deep oil is black, shallow oil is mid-grey
                                    //if (redcol < 0) redcol = 0; if (greencol < 0) greencol = 0; if (bluecol < 0) bluecol = 0;
                                    //if (redcol > 255) redcol = 255; if (greencol > 255) greencol = 255; if (bluecol > 255) bluecol = 255;

                                    SolidBrush brush = new SolidBrush(Color.FromArgb(alphacol, redcol, greencol, bluecol));
                                    objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                                }
                            }
                        }

                        // Definizione della scala di colori Viridis
                        Color[] viridisColors = new Color[]
                        {
                             Color.FromArgb(68, 1, 84),
                             Color.FromArgb(68, 2, 85),
                             Color.FromArgb(65, 40, 104),
                             Color.FromArgb(60, 81, 139),
                             Color.FromArgb(50, 113, 176),
                             Color.FromArgb(32, 144, 203),
                             Color.FromArgb(14, 184, 227),
                             Color.FromArgb(253, 231, 37)
                        };

                        // Funzione per ottenere il colore dalla scala Viridis
                        Color GetViridisColor(double value, double minValue, double maxValue)
                        {

                            double normalizedValue = (value - minValue) / (maxValue - minValue);
                            normalizedValue = Math.Max(0, Math.Min(1, normalizedValue));

                            // Calcola l'indice del colore
                            int colorIndex = (int)(normalizedValue * (viridisColors.Length - 1));
                            return viridisColors[colorIndex];
                        }

                        // Oil concentration - OIL_V1_PDF
                        if (menuItemOilconcVisualisation.Checked == true && isOilSimulation == true)
                        {
                            if (index[x, y] != -9999)
                            {
                                if (oil_concentration[x, y] > 0.000001 && water_depth[x, y] > water_depth_erosion_threshold && oil_depth[x, y] > 0.00001)


                                {
                                    // Viridis scale colours
                                    Color viridisColor = GetViridisColor(oil_concentration[x, y], 0.00001, oilCRange);


                                    if (comboBox1.Text == "oil concentration") // display only the oil concentration for this cell
                                    {
                                        alphacol = 255;
                                    }
                                    else // display on top of whatever else is selected (semi-transparent cell)
                                    {
                                        alphacol = 128;
                                    }


                                    SolidBrush brush = new SolidBrush(Color.FromArgb(alphacol, viridisColor.R, viridisColor.G, viridisColor.B));
                                    objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                                }
                            }
                        }

                        /*// Oil spill - OIL_V1_MDW
                        if (menuItemOilconcVisualisation.Checked == true)
                        {
                            if (index[x, y] != -9999)
                            {
                                if (oil_conc[x, y] > 0.000001 && water_depth[x, y] > water_depth_erosion_threshold && oil_depth[x, y] > 0.0001)
                                {
                                    // Normalise oilc onc
                                    double normalizedCOilDepth = (oil_conc[x, y] - oilCMin) / oilCRange;
                                    if (normalizedCOilDepth < 0) normalizedCOilDepth = 0;
                                    if (normalizedCOilDepth > 1) normalizedCOilDepth = 1;

                                    
                                    int r = (int)(255 * (0.267004 * normalizedCOilDepth + 0.004888));
                                    int g = (int)(255 * (0.004888 * normalizedCOilDepth + 0.232325));
                                    int b = (int)(255 * (0.093249 * normalizedCOilDepth + 0.138157));

                                    // Selezioniamo l'alpha in base al tipo di visualizzazione
                                    if (comboBox1.Text == "oil concentration") // visualizza solo l'olio come cella opaca
                                    {
                                        alphacol = 255;
                                    }
                                    else // visualizza sopra qualsiasi altro elemento (cella semi-trasparente)
                                    {
                                        alphacol = 128;
                                    }

                                    // Assicurati che i colori siano nel range 0-255
                                    if (r < 0) r = 0; if (g < 0) g = 0; if (b < 0) b = 0;
                                    if (r > 255) r = 255; if (g > 255) g = 255; if (b > 255) b = 255;

                                    SolidBrush brush = new SolidBrush(Color.FromArgb(alphacol, r, g, b));
                                    objGraphics.FillRectangle(brush, (x - 1) * t, (y - 1) * t, t, t);
                                }
                            }
                        }*/




                    }           // Close of nodata check for 'active' grid only
                }               // Close of Collumn Loop
            }                   // Close of Row Loop


            this.QsStatusPanel.Text = string.Format("Qs = {0:F8}", tomsedi * DX * DX);

            objGraphics.Dispose();
            zoomPanImageBox1.Image = m_objDrawingSurface;

        }   // Close of drawwater() // <JOE 20051605- end>

        double erode(double mult_factor)
        {
            double rho = 1000.0;
            //double gravity = 9.8;
            double tempbmax = 0;

            double[,] gtot2;

            gtot2 = new Double[20, tracers + 1];

            for (int n = 0; n <= G_MAX; n++)
            {
                for (int T = 0; T <= tracers; T++) gtot2[n, T] = 0;
            }

            time_factor = time_factor * 1.5;
            if (time_factor > max_time_step) time_factor = max_time_step;

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    // zero vels.
                    Vel[x, y] = 0;
                    Tau[x, y] = 0;
                    erodetot[x, y] = 0;
                    erodetot3[x, y] = 0;
                    temp_elev[x, y] = 0;

                    for (int n = 0; n < G_MAX; n++)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            sr[x, y, n, T] = 0;
                            sl[x, y, n, T] = 0;
                            su[x, y, n, T] = 0;
                            sd[x, y, n, T] = 0;
                        }
                    }
                    for (int T = 0; T <= tracers; T++) ss[x, y, T] = 0;


                    if (water_depth[x, y] > water_depth_erosion_threshold)
                    {
                        double veltot = 0;
                        double vel = 0;
                        double qtot = 0;
                        double tau = 0;
                        double velnum = 0;
                        double slopetot = 0;



                        // add spatial mannings here
                        double temp_mannings = mannings;
                        if (SpatVarManningsCheckbox.Checked == true) temp_mannings = spat_var_mannings[x, y];

                        // check to see if index for that cell...
                        if (index[x, y] == -9999) addGS(x, y);

                        // now tot up velocity directions, velocities and edge directions.
                        for (int p = 1; p <= 8; p += 2)
                        {
                            int x2 = x + deltaX[p];
                            int y2 = y + deltaY[p];
                            if (water_depth[x2, y2] > water_depth_erosion_threshold)
                            {

                                if (vel_dir[x, y, p] > 0)
                                {
                                    vel = vel_dir[x, y, p];
                                    if (vel > max_vel)
                                    {
                                        this.tempStatusPanel.Text = Convert.ToString(x) + " " + Convert.ToString(y) + " " + Convert.ToString(vel);
                                        vel = max_vel; // if vel too high cut it
                                    }

                                    veltot += vel * vel;
                                    velnum++;
                                    qtot += (vel * vel);
                                    //slopetot += ((elev[x, y] - elev[x2, y2]) / DX);
                                    slopetot += ((elev[x, y] - elev[x2, y2]) / DX) * vel;
                                }
                            }
                        }

                        if (qtot > 0)
                        {
                            vel = (Math.Sqrt(qtot));
                            Vel[x, y] = vel;
                            if (vel < 0)
                            {
                                this.tempStatusPanel.Text = Convert.ToString(x) + " " + Convert.ToString(y) + " " + Convert.ToString(vel);
                            }
                            if (vel > max_vel) vel = max_vel; // if vel too high cut it
                            double ci = gravity * (temp_mannings * temp_mannings) * Math.Pow(water_depth[x, y], -0.33);
                            //tauvel = 1000 * ci * vel * vel;
                            if (slopetot > 0) slopetot = 0;
                            //tauvel = 1000 * ci * vel * vel * (1 + (1 * (slopetot)));
                            tau = 1000 * ci * vel * vel * (1 + (1 * (slopetot / vel)));
                            Tau[x, y] = tau;
                        }
                    }
                }
            });


            int counter2 = 0;
            do
            {
                counter2++;
                tempbmax = 0;
                double[] tempbmax2;
                tempbmax2 = new Double[ymax + 2];

                //var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount *  4 };
                Parallel.For(1, ymax, options, delegate (int y)
                {
                    int inc = 1;
                    while (down_scan[y, inc] > 0)
                    {
                        int x = down_scan[y, inc];
                        inc++;

                        // now do some erosion
                        if (Tau[x, y] > 0)
                        {
                            double d_50 = 0;
                            double Fs = 0;
                            double Di = 0;
                            double graintot = 0;
                            double tau = Tau[x, y];
                            double temp_tau_ri = 0;
                            double U_star = 0;
                            double U_star_cubed = 0;

                            double[] temp_dist, tempdir;
                            double[,] tracer_proportions;
                            tempdir = new Double[11]; // array that holds velocity directions temp - so they dont have to be calculated again
                            temp_dist = new Double[11]; // array that holds amount to be removed from cell in each grainsize
                            tracer_proportions = new double[11, tracers + 1];

                            // additional tracer code.
                            for (int n = 1; n <= G_MAX - 1; n++) // now work out the proportion of the different tracer fractions for each grainsize in the donor cell.
                            {
                                double temptot = 0;
                                for (int T = 0; T <= tracers; T++) temptot += grain[index[x, y], n, T];
                                if (temptot <= 0) temptot = 1;
                                for (int T = 0; T <= tracers; T++)
                                {
                                    tracer_proportions[n, T] = grain[index[x, y], n, T] / temptot;
                                }
                            }

                            if (wilcock == 1)
                            {
                                d_50 = d50(index[x, y]);
                                if (d_50 < d1) d_50 = d1;
                                Fs = sand_fraction(index[x, y]);
                                for (int T = 0; T <= tracers; T++) for (int n = 1; n <= G_MAX; n++) graintot += (grain[index[x, y], n, T]);
                                temp_tau_ri = (0.021 + (0.015 * Math.Exp(-20 * Fs))) * (rho * gravity * d_50);
                                U_star = Math.Pow(tau / rho, 0.5);
                                U_star_cubed = U_star * U_star * U_star;
                            }
                            //////
                            double temptot1 = 0;

                            for (int n = 1; n <= G_MAX - 1; n++)
                            {

                                switch (n)
                                {
                                    case 1: Di = d1; break;
                                    case 2: Di = d2; break;
                                    case 3: Di = d3; break;
                                    case 4: Di = d4; break;
                                    case 5: Di = d5; break;
                                    case 6: Di = d6; break;
                                    case 7: Di = d7; break;
                                    case 8: Di = d8; break;
                                    case 9: Di = d9; break;
                                }

                                // Wilcock and Crowe/Curran


                                if (wilcock == 1)
                                {
                                    Double tau_ri = 0, Wi_star;
                                    tau_ri = temp_tau_ri * Math.Pow((Di / d_50), (0.67 / (1 + Math.Exp(1.5 - (Di / d_50)))));
                                    double temptot5 = 0;
                                    for (int T = 0; T <= tracers; T++) temptot5 += grain[index[x, y], n, T];
                                    double Fi = temptot5 / graintot;

                                    if ((tau / tau_ri) < 1.35)
                                    {
                                        Wi_star = 0.002 * Math.Pow(tau / tau_ri, 7.5);
                                    }
                                    else
                                    {
                                        Wi_star = 14 * Math.Pow(1 - (0.894 / Math.Pow(tau / tau_ri, 0.5)), 4.5);
                                    }
                                    //maybe should divide by DX as well..
                                    temp_dist[n] = mult_factor * time_factor *
                                        ((Fi * (U_star_cubed)) / ((2.65 - 1) * gravity)) * Wi_star / DX;
                                }
                                // Einstein sed tpt eqtn
                                if (einstein == 1)
                                {
                                    // maybe should divide by DX as well..
                                    temp_dist[n] = mult_factor * time_factor * (40 * Math.Pow((1 / (((2650 - 1000) * Di) / (tau / gravity))), 3))
                                        / Math.Sqrt(1000 / ((2650 - 1000) * gravity * (Di * Di * Di))) / DX;
                                }

                                // Meyer-Peter_Muller set tpt eqtn https://en.m.wikipedia.org/wiki/Sediment_transport#Meyer-Peter_M.C3.BCller_and_derivatives
                                // here using Wiberg and Dungan 89 modification https://dx.doi.org/10.1061%2F%28ASCE%290733-9429%281989%29115%3A1%28101%29
                                if (meyer == 1)
                                {
                                    double TauStar = tau / ((2650 - 1000) * gravity * Di);
                                    if (TauStar > 0.047)
                                    {
                                        temp_dist[n] = mult_factor * time_factor * 9.64 * Math.Pow(TauStar, 0.166) * Math.Pow((TauStar - 0.047), 1.5);
                                    }

                                }



                                //if (temp_dist[n] < 0.0000000000001) temp_dist[n] = 0;

                                // first check to see that theres not too little sediment in a cell to be entrained
                                double temptot = 0;
                                for (int T = 0; T <= tracers; T++) temptot += grain[index[x, y], n, T];
                                if (temp_dist[n] > temptot) temp_dist[n] = temptot;
                                // then check to see if this would make SS levels too high.. and if so reduce


                                if (isSuspended[n] && n == 1)
                                {
                                    for (int T = 0; T <= tracers; T++)
                                    {
                                        if (((temp_dist[n] + Vsusptot[x, y, T]) * tracer_proportions[n, T]) / water_depth[x, y] > Csuspmax)
                                        {
                                            //work out max amount of sediment that can be there (waterdepth * csuspmax) then subtract whats already there
                                            // (Vsusptot) to leave what can be entrained. Check if < 0 after.
                                            temp_dist[n] = (water_depth[x, y] * Csuspmax * tracer_proportions[n, T]) - Vsusptot[x, y, T]; //// DOUBLE CHECK I've got this right TC May2020
                                        }
                                    }
                                }
                                if (temp_dist[n] < 0) temp_dist[n] = 0;

                                // nwo placed here speeding up reduction of erode repeats.
                                temptot1 += temp_dist[n];

                            }


                            //check if this makes it below bedrock
                            if (elev[x, y] - temptot1 <= bedrock[x, y])
                            {
                                // now remove from proportion that can be eroded..
                                // we can do this as we have the prop (in temptot) that is there to be eroded.
                                double elevdiff = elev[x, y] - bedrock[x, y];
                                double temptot3 = temptot1;
                                temptot1 = 0;
                                for (int n = 1; n <= G_MAX - 1; n++)
                                {
                                    if (elev[x, y] <= bedrock[x, y])
                                    {
                                        temp_dist[n] = 0;
                                    }
                                    else
                                    {
                                        temp_dist[n] = elevdiff * (temp_dist[n] / temptot3);
                                        if (temp_dist[n] < 0) temp_dist[n] = 0;
                                    }
                                    temptot1 += temp_dist[n];
                                }

                                // here insert bedrock erosion routine?
                                if (tau > bedrock_erosion_threshold)
                                {
                                    double amount = 0; // amount is amount of erosion into the bedrock.
                                    amount = Math.Pow(bedrock_erosion_rate * tau, 1.5) * time_factor * mult_factor * 0.000000317; // las value to turn it into erosion per year (number of years per second)
                                    bedrock[x, y] -= amount;
                                    // now add amount of bedrock eroded into sediment proportions.
                                    for (int n2 = 1; n2 <= G_MAX - 1; n2++)
                                    {
                                        if (grain_area[x, y] == 0)
                                        { grain[index[x, y], n2, 0] += amount * dprop[n2]; }
                                        else if (grain_area[x, y] == 1)
                                        { grain[index[x, y], n2, 0] += amount * dprop_[n2]; }
                                    }
                                }
                            }


                            // veg components
                            // here to erode the veg layer..
                            if (veg[x, y, 1] > 0 && tau > vegTauCrit)
                            {
                                // now to remove from veg layer..
                                veg[x, y, 1] -= mult_factor * time_factor * Math.Pow(tau - vegTauCrit, 0.5) * 0.00001;
                                if (veg[x, y, 1] < 0) veg[x, y, 1] = 0;
                            }

                            //// now to determine if movement should be restricted due to veg... (old method)
                            if (radioButton1.Checked && veg[x, y, 1] > 0.25)
                            {
                                // now checks if this removed from the cell would put it below the veg layer..
                                if (elev[x, y] - temptot1 <= veg[x, y, 0])
                                {
                                    // now remove from proportion that can be eroded..
                                    // we can do this as we have the prop (in temptot) that is there to be eroded.
                                    double elevdiff = 0;
                                    elevdiff = elev[x, y] - veg[x, y, 0];
                                    if (elevdiff < 0) elevdiff = 0;
                                    double temptot3 = temptot1;
                                    temptot1 = 0;
                                    for (int n = 1; n <= G_MAX - 1; n++)
                                    {
                                        temp_dist[n] = elevdiff * (temp_dist[n] / temptot3);
                                        if (elev[x, y] <= veg[x, y, 0]) temp_dist[n] = 0;
                                        temptot1 += temp_dist[n];
                                    }
                                    //temptot1 -= elevdiff;
                                    if (temptot1 < 0) temptot1 = 0;

                                }
                                //tempStatusPanel.Text = Convert.ToString(1);
                            }

                            //// now to determine if movement should be restricted due to veg... (new method)
                            if (radioButton2.Checked && veg[x, y, 1] > 0.1)
                            {
                                temptot1 = 0;
                                for (int n = 1; n <= G_MAX - 1; n++)
                                {
                                    temp_dist[n] *= 1 - (veg[x, y, 1] * (1 - veg_lat_restriction));
                                    temptot1 += temp_dist[n];
                                }
                                //temptot1 -= elevdiff;
                                if (temptot1 < 0) temptot1 = 0;

                                //tempStatusPanel.Text = Convert.ToString(2);
                            }

                            if (temptot1 > tempbmax2[y]) tempbmax2[y] = temptot1;
                            //tempStatusPanel.Text = Convert.ToString(temptot1);

                            // now work out what portion of bedload has to go where...
                            // only allow actual transfer of sediment if there is flow in a direction - i.e. some sedeiment transport

                            // wonder if this part could be separately parallelised?
                            if (temptot1 > 0)
                            {
                                double temptot2 = 0;
                                double veltot = 0;
                                for (int p = 1; p <= 8; p += 2)
                                {
                                    int x2 = x + deltaX[p];
                                    int y2 = y + deltaY[p];
                                    if (water_depth[x2, y2] > water_depth_erosion_threshold)
                                    {
                                        if (edge[x, y] > edge[x2, y2])
                                        {
                                            temptot2 += (edge[x, y] - edge[x2, y2]);
                                        }

                                        if (vel_dir[x, y, p] > 0)
                                        {
                                            // first work out velocities in each direction (for sedi distribution)
                                            double vel = vel_dir[x, y, p];
                                            tempdir[p] = vel * vel;
                                            veltot += tempdir[p];
                                        }
                                    }
                                }


                                for (int p = 1; p <= 8; p += 2)
                                {
                                    int x2 = x + deltaX[p];
                                    int y2 = y + deltaY[p];



                                    if (water_depth[x2, y2] > water_depth_erosion_threshold)
                                    {
                                        if (index[x2, y2] == -9999) addGS(x2, y2);
                                        double factor = 0;

                                        // vel slope
                                        if (vel_dir[x, y, p] > 0)
                                        {
                                            factor += 0.75 * tempdir[p] / veltot;
                                        }
                                        // now for lateral gradient.
                                        if (edge[x, y] > edge[x2, y2])
                                        {
                                            factor += 0.25 * ((edge[x, y] - edge[x2, y2]) / temptot2);
                                        }

                                        // now loop through grainsizes
                                        for (int n = 1; n <= G_MAX - 1; n++)
                                        {
                                            if (temp_dist[n] > 0)
                                            {
                                                if (n == 1 && isSuspended[n])
                                                {
                                                    // put amount entrained by ss in to ss[,]
                                                    for (int T = 0; T <= tracers; T++) ss[x, y, T] = temp_dist[n] * tracer_proportions[n, T];
                                                }
                                                else
                                                {
                                                    switch (p)
                                                    {
                                                        case 1: for (int T = 0; T <= tracers; T++) su[x, y, n, T] = temp_dist[n] * tracer_proportions[n, T] * factor; break;
                                                        case 3: for (int T = 0; T <= tracers; T++) sr[x, y, n, T] = temp_dist[n] * tracer_proportions[n, T] * factor; break;
                                                        case 5: for (int T = 0; T <= tracers; T++) sd[x, y, n, T] = temp_dist[n] * tracer_proportions[n, T] * factor; break;
                                                        case 7: for (int T = 0; T <= tracers; T++) sl[x, y, n, T] = temp_dist[n] * tracer_proportions[n, T] * factor; break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });

                // we have to do a reduction on tempbmax.
                for (int y = 1; y <= ymax; y++) if (tempbmax2[y] > tempbmax) tempbmax = tempbmax2[y];

                if (tempbmax > ERODEFACTOR)
                {
                    time_factor *= (ERODEFACTOR / tempbmax) * 0.5;
                }
            } while (tempbmax > ERODEFACTOR);

            //tempStatusPanel.Text = Convert.ToString(counter2);

            //
            // new temp erode array.


            var options1 = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(2, ymax, options1, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;


                    if (water_depth[x, y] > water_depth_erosion_threshold && x < xmax && x > 1)
                    {

                        if (index[x, y] == -9999) addGS(x, y);
                        for (int n = 1; n <= G_MAX - 1; n++)
                        {
                            if (n == 1 && isSuspended[n])
                            {
                                // updating entrainment of SS
                                for (int T = 0; T <= tracers; T++)
                                {
                                    Vsusptot[x, y, T] += ss[x, y, T];
                                    grain[index[x, y], n, T] -= ss[x, y, T];
                                    erodetot[x, y] -= ss[x, y, T];
                                }

                                // this next part is unusual. You have to stop susp sed deposition on the input cells, otherwies
                                // it drops sediment out, but cannot entrain as ss levels in input are too high leading to
                                // little mountains of sediment. This means a new array in order to check whether a cell is an
                                // input point or not..
                                if (!inputpointsarray[x, y])
                                {
                                    // now calc ss to be dropped
                                    double coeff = (fallVelocity[n] * time_factor * mult_factor) / water_depth[x, y];
                                    if (coeff > 1) coeff = 1;
                                    for (int T = 0; T <= tracers; T++)
                                    {
                                        double Vpdrop = coeff * Vsusptot[x, y, T];
                                        if (Vpdrop > 0.001) Vpdrop = 0.001; //only allow 1mm to be deposited per iteration
                                        grain[index[x, y], n, T] += Vpdrop;
                                        erodetot[x, y] += Vpdrop;
                                        Vsusptot[x, y, T] -= Vpdrop;
                                    }
                                    //if (Vsusptot[x, y] < 0) Vsusptot[x, y] = 0; NOT this line.
                                }
                            }
                            else
                            {
                                //else update grain and elevations for bedload.
                                for (int T = 0; T <= tracers; T++)
                                {
                                    double val1 = (su[x, y, n, T] + sr[x, y, n, T] + sd[x, y, n, T] + sl[x, y, n, T]);
                                    double val2 = (su[x, y + 1, n, T] + sd[x, y - 1, n, T] + sl[x + 1, y, n, T] + sr[x - 1, y, n, T]);
                                    grain[index[x, y], n, T] += val2 - val1;
                                    erodetot[x, y] += val2 - val1;
                                    erodetot3[x, y] += val1;
                                }
                            }
                        }

                        temp_elev[x, y] += erodetot[x, y];
                        if (erodetot[x, y] != 0) sort_active(x, y);

                        //
                        // test lateral code...
                        //

                        if (erodetot3[x, y] > 0)
                        {

                            if (elev[x - 1, y] > elev[x, y] && x > 2)
                            {
                                double amt = 0;

                                if (water_depth[x - 1, y] < water_depth_erosion_threshold)
                                    amt = mult_factor * lateral_constant * Tau[x, y] * edge[x - 1, y] * time_factor / DX;
                                else amt = bed_proportion * erodetot3[x, y] * (elev[x - 1, y] - elev[x, y]) / DX * 0.1;

                                if (amt > 0)
                                {
                                    amt *= 1 - (veg[x - 1, y, 1] * (1 - veg_lat_restriction));
                                    if ((elev[x - 1, y] - amt) < bedrock[x - 1, y] || x - 1 == 1) amt = 0;
                                    if (amt > ERODEFACTOR * 0.1) amt = ERODEFACTOR * 0.1;
                                    //if (amt > erodetot2 / 2) amt = erodetot2 / 2;
                                    if (amt < 0) amt = 0;
                                    temp_elev[x, y] += amt;
                                    temp_elev[x - 1, y] -= amt;
                                    slide_GS(x - 1, y, amt, x, y);
                                }
                            }
                            if (elev[x + 1, y] > elev[x, y] && x < xmax - 1)
                            {
                                double amt = 0;
                                if (water_depth[x + 1, y] < water_depth_erosion_threshold)
                                    amt = mult_factor * lateral_constant * Tau[x, y] * edge[x + 1, y] * time_factor / DX;
                                else amt = bed_proportion * erodetot3[x, y] * (elev[x + 1, y] - elev[x, y]) / DX * 0.1;

                                if (amt > 0)
                                {
                                    amt *= 1 - (veg[x + 1, y, 1] * (1 - veg_lat_restriction));
                                    if ((elev[x + 1, y] - amt) < bedrock[x + 1, y] || x + 1 == xmax) amt = 0;
                                    if (amt > ERODEFACTOR * 0.1) amt = ERODEFACTOR * 0.1;
                                    //if (amt > erodetot2 /2) amt = erodetot2 /2;
                                    if (amt < 0) amt = 0;
                                    temp_elev[x, y] += amt;
                                    temp_elev[x + 1, y] -= amt;
                                    slide_GS(x + 1, y, amt, x, y);
                                }
                            }


                        }
                    }
                }
            });

            Parallel.For(2, xmax, options1, delegate (int x)
            {
                int inc = 1;
                while (cross_scan[x, inc] > 0)
                {
                    int y = cross_scan[x, inc];
                    inc++;

                    {

                        if (erodetot3[x, y] > 0)
                        {
                            if (elev[x, y - 1] > elev[x, y])
                            {
                                double amt = 0;
                                if (water_depth[x, y - 1] < water_depth_erosion_threshold)
                                    amt = mult_factor * lateral_constant * Tau[x, y] * edge[x, y - 1] * time_factor / DX;
                                else amt = bed_proportion * erodetot3[x, y] * (elev[x, y - 1] - elev[x, y]) / DX * 0.1;

                                if (amt > 0)
                                {
                                    amt *= 1 - (veg[x, y - 1, 1] * (1 - veg_lat_restriction));
                                    if ((elev[x, y - 1] - amt) < bedrock[x, y - 1] || y - 1 == 1) amt = 0;
                                    if (amt > ERODEFACTOR * 0.1) amt = ERODEFACTOR * 0.1;
                                    //if (amt > erodetot2 / 2) amt = erodetot2 / 2;
                                    if (amt < 0) amt = 0;
                                    temp_elev[x, y] += amt;
                                    temp_elev[x, y - 1] -= amt;
                                    slide_GS(x, y - 1, amt, x, y);
                                }
                            }
                            if (elev[x, y + 1] > elev[x, y])
                            {
                                double amt = 0;
                                if (water_depth[x, y + 1] < water_depth_erosion_threshold)
                                    amt = amt = mult_factor * lateral_constant * Tau[x, y] * edge[x, y + 1] * time_factor / DX;
                                else amt = bed_proportion * erodetot3[x, y] * (elev[x, y + 1] - elev[x, y]) / DX * 0.1;

                                if (amt > 0)
                                {
                                    amt *= 1 - (veg[x, y + 1, 1] * (1 - veg_lat_restriction));
                                    if ((elev[x, y + 1] - amt) < bedrock[x, y + 1] || y + 1 == ymax) amt = 0;
                                    if (amt > ERODEFACTOR * 0.1) amt = ERODEFACTOR * 0.1;
                                    //if (amt > erodetot2 / 2) amt = erodetot2 / 2;
                                    if (amt < 0) amt = 0;
                                    temp_elev[x, y] += amt;
                                    temp_elev[x, y + 1] -= amt;
                                    slide_GS(x, y + 1, amt, x, y);
                                }
                            }


                        }
                    }
                }
            });

            Parallel.For(2, ymax, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];
                    inc++;

                    if (x > 1 && x < xmax) elev[x, y] += temp_elev[x, y];

                }
            });


            // now calculate sediment outputs from all four edges...
            for (int y = 2; y < ymax; y++)
            {
                if (water_depth[xmax, y] > water_depth_erosion_threshold || Vsusptot[xmax, y, 0] > 0)
                {
                    for (int n = 1; n <= G_MAX - 1; n++)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            if (isSuspended[n])
                            {
                                gtot2[n, T] += Vsusptot[xmax, y, T];
                                Vsusptot[xmax, y, T] = 0;
                            }
                            else
                            {
                                gtot2[n, T] += sr[xmax - 1, y, n, T];
                            }
                        }
                    }
                }
                if (water_depth[1, y] > water_depth_erosion_threshold || Vsusptot[1, y, 0] > 0)
                {
                    for (int n = 1; n <= G_MAX - 1; n++)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            if (isSuspended[n])
                            {
                                gtot2[n, T] += Vsusptot[1, y, T];
                                Vsusptot[1, y, T] = 0;
                            }
                            else
                            {
                                gtot2[n, T] += sl[2, y, n, T];
                            }
                        }
                    }
                }
            }

            for (int x = 2; x < xmax; x++)
            {
                if (water_depth[x, ymax] > water_depth_erosion_threshold || Vsusptot[x, ymax, 0] > 0)
                {
                    for (int n = 1; n <= G_MAX - 1; n++)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            if (isSuspended[n])
                            {
                                gtot2[n, T] += Vsusptot[x, ymax, T];
                                Vsusptot[x, ymax, T] = 0;
                            }
                            else
                            {
                                gtot2[n, T] += sd[x, ymax - 1, n, T];
                            }
                        }
                    }
                }
                if (water_depth[x, 1] > water_depth_erosion_threshold || Vsusptot[x, 1, 0] > 0)
                {
                    for (int n = 1; n <= G_MAX - 1; n++)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            if (isSuspended[n])
                            {
                                gtot2[n, T] += Vsusptot[x, 1, T];
                                Vsusptot[x, 1, T] = 0;
                            }
                            else
                            {
                                gtot2[n, T] += su[x, 2, n, T];
                            }
                        }
                    }
                }
            }

            /// now update files for outputing sediment and re-circulating...
            ///

            sediQ = 0;
            for (int n = 1; n <= G_MAX; n++)
            {
                for (int T = 0; T <= tracers; T++)
                {
                    if (temp_grain[n] < 0) temp_grain[n] = 0;
                    if (recirculatebox.Checked == true && reach_mode_box.Checked == true)
                        temp_grain[n] += gtot2[n, T] * recirculate_proportion; // important to divide input by time factor, so it can be reduced if re-circulating too much...
                    sediQ += gtot2[n, T] * DX * DX;
                    globalsediq += gtot2[n, T] * DX * DX;
                    sum_grain[n, T] += gtot2[n, T] * DX * DX; // Gez
                }
            }

            return tempbmax;

        }

        private void landslide_grainsize_CheckedChanged(object sender, EventArgs e)
        {
            this.label30.Visible = true;
            this.label32.Visible = true;
            this.label118.Visible = true;
            this.label110.Visible = true;
            this.label111.Visible = true;
            this.label112.Visible = true;
            this.label113.Visible = true;
            this.label114.Visible = true;
            this.label115.Visible = true;
            this.label116.Visible = true;
            this.label117.Visible = true;
            this.label120.Visible = true;
            this.g1_box.Visible = true;
            this.g2_box.Visible = true;
            this.g3_box.Visible = true;
            this.g4_box.Visible = true;
            this.g5_box.Visible = true;
            this.g6_box.Visible = true;
            this.g7_box.Visible = true;
            this.g8_box.Visible = true;
            this.g9_box.Visible = true;
            this.gp1_box.Visible = true;
            this.gp2_box.Visible = true;
            this.gp3_box.Visible = true;
            this.gp4_box.Visible = true;
            this.gp5_box.Visible = true;
            this.gp6_box.Visible = true;
            this.gp7_box.Visible = true;
            this.gp8_box.Visible = true;
            this.gp9_box.Visible = true;

        }

        void slide_3()
        {
            int x, y, inc;
            double wet_factor;
            double factor = Math.Tan((failureangle * (3.141592654 / 180))) * DX;
            double diff = 0;

            for (y = 2; y < ymax; y++)
            {
                inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    x = down_scan[y, inc];
                    if (x == xmax) x = xmax - 1;
                    if (x == 1) x = 2;

                    inc++;
                    /** check to see if under water **/
                    wet_factor = factor;
                    //if(water_depth[x,y]>0.01)wet_factor=factor/2;
                    if (elev[x, y] <= (bedrock[x, y] + active)) wet_factor = 10000;

                    /** chexk landslides in channel slowly */

                    if (((elev[x, y] - elev[x + 1, y + 1]) / 1.41) > wet_factor && elev[x + 1, y + 1] > -9999)
                    {
                        diff = ((elev[x, y] - elev[x + 1, y + 1]) / 1.41) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x + 1, y + 1] += diff;
                        slide_GS(x, y, diff, x + 1, y + 1);
                    }
                    if ((elev[x, y] - elev[x, y + 1]) > wet_factor && elev[x, y + 1] > -9999)
                    {
                        diff = (elev[x, y] - elev[x, y + 1]) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x, y + 1] += diff;
                        slide_GS(x, y, diff, x, y + 1);
                    }
                    if (((elev[x, y] - elev[x - 1, y + 1]) / 1.41) > wet_factor && elev[x - 1, y + 1] > -9999)
                    {
                        diff = ((elev[x, y] - elev[x - 1, y + 1]) / 1.41) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x - 1, y + 1] += diff;
                        slide_GS(x, y, diff, x - 1, y + 1);
                    }
                    if ((elev[x, y] - elev[x - 1, y]) > wet_factor && elev[x - 1, y] > -9999)
                    {
                        diff = (elev[x, y] - elev[x - 1, y]) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x - 1, y] += diff;
                        slide_GS(x, y, diff, x - 1, y);
                    }

                    if (((elev[x, y] - elev[x - 1, y - 1]) / 1.41) > wet_factor && elev[x - 1, y - 1] > -9999)
                    {
                        diff = ((elev[x, y] - elev[x - 1, y - 1]) / 1.41) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x - 1, y - 1] += diff;
                        slide_GS(x, y, diff, x - 1, y - 1);
                    }
                    if ((elev[x, y] - elev[x, y - 1]) > wet_factor && elev[x, y - 1] > -9999)
                    {
                        diff = (elev[x, y] - elev[x, y - 1]) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x, y - 1] += diff;
                        slide_GS(x, y, diff, x, y - 1);
                    }
                    if (((elev[x, y] - elev[x + 1, y - 1]) / 1.41) > wet_factor && elev[x + 1, y - 1] > -9999)
                    {
                        diff = ((elev[x, y] - elev[x + 1, y - 1]) / 1.41) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x + 1, y - 1] += diff;
                        slide_GS(x, y, diff, x + 1, y - 1);
                    }

                    if ((elev[x, y] - elev[x + 1, y]) > wet_factor && elev[x + 1, y] > -9999)
                    {
                        diff = (elev[x, y] - elev[x + 1, y]) - wet_factor;
                        if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                        if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                        if (diff < 0) diff = 0;
                        elev[x, y] -= diff;
                        elev[x + 1, y] += diff;
                        slide_GS(x, y, diff, x + 1, y);
                    }

                }
            }

        }

        private void tracer_file_TextChanged(object sender, EventArgs e)
        {

        }

        private void menuItem16_Click(object sender, EventArgs e)
        {
            menuItem16.Checked = (!menuItem16.Checked);
        }

        private void tracerOutcheckBox_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void checkBox_tracer_CheckedChanged(object sender, EventArgs e)
        {
            this.label106.Visible = true;
            this.label107.Visible = true;
            this.label122.Visible = true;
            this.label123.Visible = true;
            this.label124.Visible = true;
            this.tracer_num.Visible = true;
            this.tracer_file.Visible = true;
            this.mine_input_textBox.Visible = true;
        }

        void slide_5()
        {
            int x, y, inc = 0;
            double wet_factor;
            double factor = Math.Tan((failureangle * (3.141592654 / 180))) * DX;
            //if(landslidesBox.Checked == true) factor = DX * ((-265000 * j_mean) + 1.38);
            double diff = 0;
            double total = 0;

            if (DuneBox.Checked == true)
            {
                for (x = 1; x <= xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        elev[x, y] -= sand[x, y];
                    }
                }
            }



            do
            {
                total = 0;
                inc++;
                for (y = 2; y < ymax; y++)
                {
                    for (x = 2; x < xmax; x++)
                    {

                        wet_factor = factor;
                        //if(water_depth[x,y]>0.01)wet_factor=factor/2;
                        if (elev[x, y] >= (bedrock[x, y] + active))
                        {


                            if (((elev[x, y] - elev[x + 1, y + 1]) / 1.41) > wet_factor && elev[x + 1, y + 1] > -9999)
                            {
                                diff = ((elev[x, y] - elev[x + 1, y + 1]) / 1.41) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x + 1, y + 1] += diff;
                                slide_GS(x, y, diff, x + 1, y + 1);
                                total += diff;
                            }
                            if ((elev[x, y] - elev[x, y + 1]) > wet_factor && elev[x, y + 1] > -9999)
                            {
                                diff = (elev[x, y] - elev[x, y + 1]) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x, y + 1] += diff;
                                slide_GS(x, y, diff, x, y + 1);
                                total += diff;
                            }
                            if (((elev[x, y] - elev[x - 1, y + 1]) / 1.41) > wet_factor && elev[x - 1, y + 1] > -9999)
                            {
                                diff = ((elev[x, y] - elev[x - 1, y + 1]) / 1.41) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x - 1, y + 1] += diff;
                                slide_GS(x, y, diff, x - 1, y + 1);
                                total += diff;
                            }
                            if ((elev[x, y] - elev[x - 1, y]) > wet_factor && elev[x - 1, y] > -9999)
                            {
                                diff = (elev[x, y] - elev[x - 1, y]) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x - 1, y] += diff;
                                slide_GS(x, y, diff, x - 1, y);
                                total += diff;
                            }

                            if (((elev[x, y] - elev[x - 1, y - 1]) / 1.41) > wet_factor && elev[x - 1, y - 1] > -9999)
                            {
                                diff = ((elev[x, y] - elev[x - 1, y - 1]) / 1.41) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x - 1, y - 1] += diff;
                                slide_GS(x, y, diff, x - 1, y - 1);
                                total += diff;
                            }
                            if ((elev[x, y] - elev[x, y - 1]) > wet_factor && elev[x, y - 1] > -9999)
                            {
                                diff = (elev[x, y] - elev[x, y - 1]) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x, y - 1] += diff;
                                slide_GS(x, y, diff, x, y - 1);
                                total += diff;
                            }
                            if (((elev[x, y] - elev[x + 1, y - 1]) / 1.41) > wet_factor && elev[x + 1, y - 1] > -9999)
                            {
                                diff = ((elev[x, y] - elev[x + 1, y - 1]) / 1.41) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x + 1, y - 1] += diff;
                                slide_GS(x, y, diff, x + 1, y - 1);
                                total += diff;
                            }



                            if ((elev[x, y] - elev[x + 1, y]) > wet_factor && elev[x + 1, y] > -9999)
                            {
                                diff = (elev[x, y] - elev[x + 1, y]) - wet_factor;
                                if ((elev[x, y] - diff) < (bedrock[x, y] + active)) diff = (elev[x, y] - (bedrock[x, y] + active));
                                if (diff > ERODEFACTOR) diff = ERODEFACTOR;
                                if (diff < 0) diff = 0;
                                elev[x, y] -= diff;
                                elev[x + 1, y] += diff;
                                slide_GS(x, y, diff, x + 1, y);
                                total += diff;
                            }
                        }
                    }
                }
            } while (total > 0 && inc < 200);

            if (DuneBox.Checked == true)
            {
                for (x = 1; x <= xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        elev[x, y] += sand[x, y];
                    }
                }
            }

        }

        void slide_4(int x, int y) // landslides from sand dunes...
        {
            double wet_factor;
            double factor = Math.Tan((double.Parse(textBox10.Text) * (3.141592654 / 180))) * (DX / dune_mult);
            double diff = 0;

            wet_factor = factor;



            if ((((elev2[x, y] + sand2[x, y]) - (elev2[x + 1, y + 1] + sand2[x + 1, y + 1])) / 1.41) > wet_factor && (elev2[x + 1, y + 1] + sand2[x + 1, y + 1]) > 0)
            {
                diff = (((elev2[x, y] + sand2[x, y]) - (elev2[x + 1, y + 1] + sand2[x + 1, y + 1])) / 1.41) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x + 1, y + 1] += diff;
            }
            if (((elev2[x, y] + sand2[x, y]) - (elev2[x, y + 1] + sand2[x, y + 1])) > wet_factor && (elev2[x, y + 1] + sand2[x, y + 1]) > 0)
            {
                diff = ((elev2[x, y] + sand2[x, y]) - (elev2[x, y + 1] + sand2[x, y + 1])) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x, y + 1] += diff;
            }
            if ((((elev2[x, y] + sand2[x, y]) - (elev2[x - 1, y + 1] + sand2[x - 1, y + 1])) / 1.41) > wet_factor && (elev2[x - 1, y + 1] + sand2[x - 1, y + 1]) > 0)
            {
                diff = (((elev2[x, y] + sand2[x, y]) - (elev2[x - 1, y + 1] + sand2[x - 1, y + 1])) / 1.41) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                // if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x - 1, y + 1] += diff;
            }
            if (((elev2[x, y] + sand2[x, y]) - (elev2[x - 1, y] + sand2[x - 1, y])) > wet_factor && (elev2[x - 1, y] + sand2[x - 1, y]) > 0)
            {
                diff = ((elev2[x, y] + sand2[x, y]) - (elev2[x - 1, y] + sand2[x - 1, y])) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x - 1, y] += diff;
            }

            if ((((elev2[x, y] + sand2[x, y]) - (elev2[x - 1, y - 1] + sand2[x - 1, y - 1])) / 1.41) > wet_factor && (elev2[x - 1, y - 1] + sand2[x - 1, y - 1]) > 0)
            {
                diff = (((elev2[x, y] + sand2[x, y]) - (elev2[x - 1, y - 1] + sand2[x - 1, y - 1])) / 1.41) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x - 1, y - 1] += diff;


            }
            if (((elev2[x, y] + sand2[x, y]) - (elev2[x, y - 1] + sand2[x, y - 1])) > wet_factor && (elev2[x, y - 1] + sand2[x, y - 1]) > 0)
            {
                diff = ((elev2[x, y] + sand2[x, y]) - (elev2[x, y - 1] + sand2[x, y - 1])) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x, y - 1] += diff;


            }
            if ((((elev2[x, y] + sand2[x, y]) - (elev2[x + 1, y - 1] + sand2[x + 1, y - 1])) / 1.41) > wet_factor && (elev2[x + 1, y - 1] + sand2[x + 1, y - 1]) > 0)
            {
                diff = (((elev2[x, y] + sand2[x, y]) - (elev2[x + 1, y - 1] + sand2[x + 1, y - 1])) / 1.41) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x + 1, y - 1] += diff;

            }


            if (((elev2[x, y] + sand2[x, y]) - (elev2[x + 1, y] + sand2[x + 1, y])) > wet_factor && (elev2[x + 1, y] + sand2[x + 1, y]) > 0)
            {
                diff = ((elev2[x, y] + sand2[x, y]) - (elev2[x + 1, y] + sand2[x + 1, y])) - wet_factor;
                if (diff > sand2[x, y]) diff = sand2[x, y];
                //if (((elev2[x, y] + sand2[x, y]) - diff) < (bedrock[x, y] + active)) diff = ((elev2[x, y] + sand2[x, y]) - (bedrock[x, y] + active));
                sand2[x, y] -= diff;
                sand2[x + 1, y] += diff;

            }


        }

        void slide_GS(int x, int y, double amount, int x2, int y2)
        {

            /** Ok, heres how it works, x and y are ones material moved from,
              x2 and y2 are ones material moved to...
              amd amount is the amount shifted. */

            int n;
            double total = 0;

            // do only for cells where both have grainsize..

            if (index[x, y] != -9999 && index[x2, y2] != -9999)
            {


                for (int T = 0; T <= tracers; T++)
                {
                    for (n = 1; n <= (G_MAX - 1); n++)
                    {

                        if (grain[index[x, y], n, T] > 0) total += grain[index[x, y], n, T];

                    }
                }

                if (amount > total)
                {
                    for (n = 1; n <= G_MAX - 1; n++)
                    {
                        // here is where you may need to add more from different tracer areas
                        //for (int T = 0; T <= tracers; T++) grain[index[x2, y2], n, T] += (amount - total) * dprop[n];
                        // maybe like below
                        int TT = tracer_area[x, y];// TT= tracer area.... //int TT = tracer number of area of donor cells[x, y];

                        if (grain_area[x, y] == 0)
                        { grain[index[x2, y2], n, TT] += (amount - total) * dprop[n]; }

                        else if (grain_area[x, y] == 1)
                        { grain[index[x2, y2], n, TT] += (amount - total) * dprop_[n]; }
                    }

                    amount = total;
                }

                if (total > 0)
                {
                    for (n = 1; n <= (G_MAX - 1); n++)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            double transferamt = amount * (grain[index[x, y], n, T] / total);
                            grain[index[x2, y2], n, T] += transferamt;
                            grain[index[x, y], n, T] -= transferamt;
                            if (grain[index[x, y], n, T] < 0) grain[index[x, y], n, T] = 0;
                        }
                    }

                }


                /* then to set active layer to correct depth before erosion, */
                sort_active(x, y);
                sort_active(x2, y2);
                return;
            }

            //now do for cells where only recieving cells have grainsize
            // just adds amount to reviving cells of normal..
            if (index[x, y] == -9999 && index[x2, y2] != -9999)
            {
                for (n = 1; n <= G_MAX - 1; n++)
                {
                    // below needs to be modded for TRACER material
                    //
                    int TT = tracer_area[x, y];// TT= tracer area.... //int TT = tracer number of area of donor cells[x, y];

                    if (grain_area[x, y] == 0)
                    { grain[index[x2, y2], n, TT] += (amount) * dprop[n]; }

                    else if (grain_area[x, y] == 1)
                    { grain[index[x2, y2], n, TT] += (amount) * dprop_[n]; }
                }

                /* then to set active layer to correct depth before erosion, */
                sort_active(x2, y2);
                return;
            }

            // now for cells whre dontaing cell has grainsize but not other...
            if (index[x, y] != -9999 && index[x2, y2] == -9999)
            {

                addGS(x2, y2); // add grainsize array for recieving cell..

                if (amount > active)
                {

                    for (n = 1; n <= G_MAX - 1; n++)
                    {
                        // below needs to be modded for TRACER material
                        //
                        int TT = tracer_area[x, y];// TT= tracer area....//int TT = tracer number of area of donor cells[x, y];

                        if (grain_area[x, y] == 0)
                        { grain[index[x2, y2], n, TT] += (amount - active) * dprop[n]; }

                        else if (grain_area[x, y] == 1)
                        { grain[index[x2, y2], n, TT] += (amount - active) * dprop_[n]; }

                    }

                    amount = active;
                }


                for (n = 1; n <= (G_MAX - 1); n++)
                {
                    for (int T = 0; T <= tracers; T++) if (grain[index[x, y], n, T] > 0) total += grain[index[x, y], n, T];
                }

                for (n = 1; n <= (G_MAX - 1); n++)
                {
                    if (total > 0)
                    {
                        for (int T = 0; T <= tracers; T++)
                        {
                            grain[index[x2, y2], n, T] += amount * (grain[index[x, y], n, T] / total);
                            if (grain[index[x, y], n, T] > 0.0001) grain[index[x, y], n, T] -= amount * (grain[index[x, y], n, T] / total);
                            if (grain[index[x, y], n, T] < 0) grain[index[x, y], n, T] = 0;
                        }
                    }

                }

                /* then to set active layer to correct depth before erosion, */
                sort_active(x, y);
                sort_active(x2, y2);
                return;
            }
        }

        double mean_ws_elev(int x, int y)
        {
            double elevtot = 0;
            int counter = 0;

            for (int dir = 1; dir <= 8; dir++)
            {
                int x2, y2;
                x2 = x + deltaX[dir];
                y2 = y + deltaY[dir];

                if (water_depth[x2, y2] > water_depth_erosion_threshold)
                {
                    elevtot += water_depth[x2, y2] + elev[x2, y2];
                    counter++;
                }

            }
            if (counter > 0)
            {
                elevtot /= counter;
                return elevtot;
            }

            else return 0;
        }

        void lateral3()
        {

            double[,] edge_temp, edge_temp2, water_depth2;
            int[,] upscale, upscale_edge;

            edge_temp = new Double[xmax + 1, ymax + 1];
            edge_temp2 = new Double[xmax + 1, ymax + 1];
            water_depth2 = new Double[xmax + 1, ymax + 1];
            upscale = new int[(xmax + 1) * 2, (ymax + 1) * 2];
            upscale_edge = new int[(xmax + 1) * 2, (ymax + 1) * 2];


            // first make water depth2 equal to water depth then remove single wet cells frmo water depth2 that have an undue influence..
            double mft = 0.1;// water_depth_erosion_threshold;//MIN_Q;// vel_dir threshold

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(1, ymax, options, delegate (int y)
            {
                int inc = 1;
                while (down_scan[y, inc] > 0)
                {
                    int x = down_scan[y, inc];

                    edge_temp[x, y] = 0;
                    if (x == 1) x++;
                    if (x == xmax) x--;
                    inc++;

                    if (Tau[x, y] > mft)
                    {
                        water_depth2[x, y] = Tau[x, y];
                        int tempcounter = 0;
                        for (int dir = 1; dir <= 8; dir++)
                        {
                            int x2, y2;
                            x2 = x + deltaX[dir];
                            y2 = y + deltaY[dir];
                            if (Tau[x2, y2] < mft) tempcounter++;
                        }
                        if (tempcounter > 6) water_depth2[x, y] = 0;
                    }
                }
            });

            // first make water depth2 equal to water depth then remove single wet cells frmo water depth2 that have an undue influence..
            //double mft = water_depth_erosion_threshold;//MIN_Q;// vel_dir threshold
            //for (int y = 2; y < ymax; y++)
            //{

            //    int inc = 1;
            //    while (down_scan[y, inc] > 0)
            //    {
            //        int x = down_scan[y, inc];

            //        edge_temp[x, y] = 0;
            //        if (x == 1) x++;
            //        if (x == xmax) x--;
            //        inc++;

            //        if (water_depth[x, y] > mft)
            //        {
            //            water_depth2[x, y] = water_depth[x, y];
            //            int tempcounter = 0;
            //            for (int dir = 1; dir <= 8; dir++)
            //            {
            //                int x2, y2;
            //                x2 = x + deltaX[dir];
            //                y2 = y + deltaY[dir];
            //                if (water_depth[x2, y2] < mft) tempcounter++;
            //            }
            //            if (tempcounter > 6) water_depth2[x, y] = 0;
            //        }
            //    }
            //}


            // first determine which cells are at the edge of the channel

            //var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 };
            Parallel.For(2, ymax, options, delegate (int y)
            {
                Parallel.For(2, xmax, options, delegate (int x)
                {
                    edge[x, y] = -9999;

                    if (water_depth2[x, y] < mft)
                    {
                        // if water depth < threshold then if its next to a wet cell then its an edge cell
                        if (water_depth2[x, y - 1] > mft ||
                            water_depth2[x - 1, y] > mft ||
                            water_depth2[x + 1, y] > mft ||
                            water_depth2[x, y + 1] > mft)
                        {
                            edge[x, y] = 0;
                        }

                        // unless its a dry cell surrounded by wet...
                        if (water_depth2[x, y - 1] > mft &&
                            water_depth2[x - 1, y] > mft &&
                            water_depth2[x + 1, y] > mft &&
                            water_depth2[x, y + 1] > mft)
                        {
                            edge[x, y] = -9999;
                            edge2[x, y] = -9999;
                        }

                        // then update upscaled grid..
                        upscale[(x * 2), (y * 2)] = 0; // if dry
                        upscale[(x * 2), (y * 2) - 1] = 0;
                        upscale[(x * 2) - 1, (y * 2)] = 0;
                        upscale[(x * 2) - 1, (y * 2) - 1] = 0;
                    }

                    // update upscaled grid with wet cells (if wet)
                    if (water_depth2[x, y] >= mft)
                    {
                        upscale[(x * 2), (y * 2)] = 1; // if wet
                        upscale[(x * 2), (y * 2) - 1] = 1;
                        upscale[(x * 2) - 1, (y * 2)] = 1;
                        upscale[(x * 2) - 1, (y * 2) - 1] = 1;
                    }
                });
            });



            // now determine edge cells on the new grid..

            Parallel.For(2, ymax * 2, options, delegate (int y)
            {
                Parallel.For(2, xmax * 2, options, delegate (int x)
                {
                    upscale_edge[x, y] = 0;
                    if (upscale[x, y] == 0)
                    {
                        if (upscale[x, y - 1] == 1 ||
                            upscale[x - 1, y] == 1 ||
                            upscale[x + 1, y] == 1 ||
                            upscale[x, y + 1] == 1)
                        {
                            upscale[x, y] = 2;
                        }
                    }
                });

            });



            // now tall up inside and outside on upscaled grid

            Parallel.For(2, ymax * 2, options, delegate (int y)
            {
                Parallel.For(2, xmax * 2, options, delegate (int x)
                {
                    if (upscale[x, y] == 2)
                    {
                        int wetcells = 0;
                        int drycells = 0;
                        int water = 0;
                        int edge_cell_counter = 1;

                        // sum up dry cells and edge cells -
                        // now manhattan neighbors
                        for (int dir = 1; dir <= 7; dir += 2)
                        {
                            int x2, y2;
                            x2 = x + deltaX[dir];
                            y2 = y + deltaY[dir];

                            if (upscale[x2, y2] == 1) wetcells += 1;
                            if (upscale[x2, y2] == 0) drycells += 1;
                            if (upscale[x2, y2] == 2) edge_cell_counter += 1;
                        }

                        if (edge_cell_counter > 3) drycells += edge_cell_counter - 2;
                        //
                        water = wetcells - drycells;
                        upscale_edge[x, y] = water;
                    }
                });

            });


            // now update normal edge array..

            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                Parallel.For(1, xmax + 1, options, delegate (int x)
                {
                    if (edge[x, y] == 0)
                    {
                        edge[x, y] = (double)(upscale_edge[(x * 2), (y * 2)] +
                            upscale_edge[(x * 2), (y * 2) - 1] +
                            upscale_edge[(x * 2) - 1, (y * 2)] +
                            upscale_edge[(x * 2) - 1, (y * 2) - 1]);
                        if (edge[x, y] > 2) edge[x, y] = 2; // important line to stop too great inside bends...
                        if (edge[x, y] < -2) edge[x, y] = -2;

                    }
                });
            });

            //then apply a smoothing filter over the top of this. here its done X number of times -

            double smoothing_times = double.Parse(avge_smoothbox.Text);
            double downstream_shift = double.Parse(downstreamshiftbox.Text);

            for (int n = 1; n <= smoothing_times + downstream_shift; n++)
            {
                //var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount *  4 };
                Parallel.For(2, ymax, options, delegate (int y)
                {
                    int inc = 1;
                    while (down_scan[y, inc] > 0)
                    {
                        int x = down_scan[y, inc];

                        edge_temp[x, y] = 0;
                        if (x == 1) x++;
                        if (x == xmax) x--;
                        if (y == 1) y++;
                        if (y == ymax) y--;
                        inc++;

                        if (edge[x, y] > -9999)
                        {
                            double mean = 0;
                            double num = 0;
                            double water_flag = 0;


                            // add in cell itself..
                            mean += edge[x, y];
                            num++;


                            for (int dir = 1; dir <= 8; dir++)
                            {
                                int x2, y2;
                                x2 = x + deltaX[dir];
                                y2 = y + deltaY[dir];
                                if (water_depth2[x2, y2] > mft) water_flag++;

                                if (n > smoothing_times && edge[x2, y2] > -9999 && water_depth2[x2, y2] < mft && mean_ws_elev(x2, y2) > mean_ws_elev(x, y))
                                {
                                    //now to mean manhattan neighbours - only if they share a wet diagonal neighbour
                                    if ((Math.Abs(deltaX[dir]) + Math.Abs(deltaY[dir])) != 2)
                                    {
                                        if (deltaX[dir] == 1 && deltaY[dir] == 0 &&
                                            (water_depth2[x + 1, y - 1] > mft ||
                                            water_depth2[x + 1, y + 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 0 && deltaY[dir] == 1 &&
                                            (water_depth2[x + 1, y + 1] > mft ||
                                            water_depth2[x - 1, y + 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == -1 && deltaY[dir] == 0 &&
                                            (water_depth2[x - 1, y - 1] > mft ||
                                            water_depth2[x - 1, y + 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 0 && deltaY[dir] == -1 &&
                                            (water_depth2[x - 1, y - 1] > mft ||
                                            water_depth2[x + 1, y - 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                    }
                                    //now non manahttan neighbours, with concected by a dry cell checked..
                                    else
                                    {
                                        if (deltaX[dir] == -1 && deltaY[dir] == -1 &&
                                            (water_depth2[x, y - 1] < mft ||
                                            water_depth2[x - 1, y] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 1 && deltaY[dir] == -1 &&
                                            (water_depth2[x, y - 1] < mft ||
                                            water_depth2[x + 1, y] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 1 && deltaY[dir] == 1 &&
                                            (water_depth2[x + 1, y] < mft ||
                                            water_depth2[x, y + 1] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == -1 && deltaY[dir] == 1 &&
                                            (water_depth2[x, y + 1] < mft ||
                                            water_depth2[x - 1, y] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                    }
                                }

                                else if (n <= smoothing_times && edge[x2, y2] > -9999 && water_depth2[x2, y2] < mft)
                                {
                                    //now to mean manhattan neighbours - only if they share a wet diagonal neighbour
                                    if ((Math.Abs(deltaX[dir]) + Math.Abs(deltaY[dir])) != 2)
                                    {
                                        if (deltaX[dir] == 1 && deltaY[dir] == 0 &&
                                            (water_depth2[x + 1, y - 1] > mft ||
                                            water_depth2[x + 1, y + 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 0 && deltaY[dir] == 1 &&
                                            (water_depth2[x + 1, y + 1] > mft ||
                                            water_depth2[x - 1, y + 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == -1 && deltaY[dir] == 0 &&
                                            (water_depth2[x - 1, y - 1] > mft ||
                                            water_depth2[x - 1, y + 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 0 && deltaY[dir] == -1 &&
                                            (water_depth2[x - 1, y - 1] > mft ||
                                            water_depth2[x + 1, y - 1] > mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                    }
                                    //now non manahttan neighbours, with concected by a dry cell checked..
                                    else
                                    {
                                        if (deltaX[dir] == -1 && deltaY[dir] == -1 &&
                                            (water_depth2[x, y - 1] < mft ||
                                            water_depth2[x - 1, y] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 1 && deltaY[dir] == -1 &&
                                            (water_depth2[x, y - 1] < mft ||
                                            water_depth2[x + 1, y] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == 1 && deltaY[dir] == 1 &&
                                            (water_depth2[x + 1, y] < mft ||
                                            water_depth2[x, y + 1] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                        if (deltaX[dir] == -1 && deltaY[dir] == 1 &&
                                            (water_depth2[x, y + 1] < mft ||
                                            water_depth2[x - 1, y] < mft))
                                        {
                                            mean += (edge[x + deltaX[dir], y + deltaY[dir]]);
                                            num++;
                                        }
                                    }
                                }
                            }
                            if (mean != 0) edge_temp[x, y] = mean / num;

                            // removes too many cells - islands etc..

                            //if(num>5&&edge[x,y]>0)edge_temp[x,y]=0;
                            //if(num+water_flag>7&&edge[x,y]>0)edge_temp[x,y]=0;

                            //remove edge effects
                            if (x < 3 || x > (xmax - 3)) edge_temp[x, y] = 0;
                            if (y < 3 || y > (ymax - 3)) edge_temp[x, y] = 0;

                        }
                    }
                });

                Parallel.For(2, ymax, options, delegate (int y)
                {
                    int inc = 1;
                    while (down_scan[y, inc] > 0)
                    {
                        int x = down_scan[y, inc];
                        //if (x == 1) x++;
                        //if (x == xmax) x--;
                        inc++;
                        if (edge[x, y] > -9999)
                        {
                            edge[x, y] = edge_temp[x, y];
                        }
                    }
                });
            }


            // trial line to remove too high inside bends,,
            Parallel.For(1, ymax + 1, options, delegate (int y)
            {
                Parallel.For(1, xmax + 1, options, delegate (int x)
                {
                    if (edge[x, y] > -9999)
                    {
                        if (edge[x, y] > 0) edge[x, y] = 0;
                        //if (edge[x, y] < -0.25) edge[x, y] = -0.25;
                        edge[x, y] = 0 - edge[x, y];
                        edge[x, y] = 1 / ((2.131 * Math.Pow(edge[x, y], -1.0794)) * DX);
                        //if (edge[x, y] > (1 / (DX * 3))) edge[x, y] = 1 / (DX * 3);
                        //edge[x, y] = 1 / edge[x, y];

                    }
                    if (water_depth[x, y] > water_depth_erosion_threshold && edge[x, y] == -9999) edge[x, y] = 0;
                });
            });

            //// now smooth across the channel..
            double tempdiff = 0;
            double counter = 0;
            do
            {
                counter++;
                //var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount *  4 };
                Parallel.For(2, ymax, options, delegate (int y)
                {
                    int inc = 1;
                    while (down_scan[y, inc] > 0)
                    {
                        int x = down_scan[y, inc];

                        edge_temp[x, y] = 0;
                        if (x == 1) x++;
                        if (x == xmax) x--;
                        inc++;
                        if (water_depth2[x, y] > mft && edge[x, y] == -9999) edge[x, y] = 0;

                        if (edge[x, y] > -9999 && water_depth2[x, y] > mft)
                        {
                            double mean = 0;
                            int num = 0;
                            for (int dir = 1; dir <= 8; dir += 2)
                            {
                                int x2, y2;
                                x2 = x + deltaX[dir];
                                y2 = y + deltaY[dir];

                                if (water_depth2[x2, y2] > mft && edge[x2, y2] == -9999) edge[x2, y2] = 0;
                                if (edge[x2, y2] > -9999)
                                {
                                    mean += (edge[x2, y2]);
                                    num++;
                                }
                            }
                            edge_temp[x, y] = mean / num;
                        }
                    }
                });

                tempdiff = 0;
                //Parallel.For(2, ymax, options, delegate (int y)
                //{

                // reduction needed here:
                for (int y = 2; y < ymax; y++)
                {
                    int inc = 1;
                    while (down_scan[y, inc] > 0)
                    {
                        int x = down_scan[y, inc];
                        if (x == 1) x++;
                        if (x == xmax) x--;
                        inc++;
                        if (edge[x, y] > -9999 && water_depth2[x, y] > mft)
                        {
                            if (Math.Abs(edge[x, y] - edge_temp[x, y]) > tempdiff) tempdiff = Math.Abs(edge[x, y] - edge_temp[x, y]);
                            edge[x, y] = edge_temp[x, y];
                        }
                    }
                }
                //});
            } while (tempdiff > lateral_cross_channel_smoothing); //this makes it loop until the averaging across the stream stabilises
                                                                  // so that the difference between the old and new values are < 0.0001
                                                                  //tempStatusPanel.Text = Convert.ToString(counter);

        }

        void add_minewaste()
        {
            for (int n = 0; n <= minesitenumber; n++)
            {
                int x = (int)mine_inputs[n, 0];
                int y = (int)mine_inputs[n, 1];
                double amt = mine_inputs[n, 2] / (DX * DX);
                int grainsizefraction = (int)mine_inputs[n, 3];
                int tracerfraction = (int)mine_inputs[n, 4];

                if (index[x, y] == -9999) addGS(x, y);
                elev[x, y] += amt;
                grain[index[x, y], grainsizefraction, tracerfraction] += amt;
                sort_active(x, y); // maybe not needed here....
            }

        }

        private void label125_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, System.EventArgs e)
        {

            zoomPanImageBox1.Height = this.Height - 225;
            zoomPanImageBox1.Width = this.Width - 20;
            //googleToggle();

            //HttpWebRequest req;
            //HttpWebResponse res;
            //try
            //{
            //    req = (HttpWebRequest) WebRequest.Create("http://www.coulthard.org.uk/");
            //    res = (HttpWebResponse) req.GetResponse();
            //}
            //catch(Exception ex)
            //{
            //    /// do nothing.
            //}

            //JMW <20040929 -start>
            this.Text = basetext;
            //DoingGraphics = false;
            //JMW <20040929 - end>


            // comment out all of the below to run normally. Leave uncommented in order to run in batch mode.
            ////////////////////////
            //////////////////////////



            ///// first load up xml file from command line:
            /////
            string temp_xml_name = " ";

            int i = 0;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (arg != "CAESAR.exe") temp_xml_name = arg;
                if (i != 0)
                {
                    Console.WriteLine(arg);
                }
                i++;
            }



            ///// then load up .xml file
            /////

            //XmlTextReader xreader;
            //String dum;

            //if (1 > 0)
            //{

            //    xreader = new XmlTextReader(temp_xml_name);

            //    //Read the file
            //    if (xreader != null)
            //    {
            //        xreader.ReadStartElement("Parms");
            //        xreader.ReadStartElement("General-Parms");
            //        try
            //        {
            //            overrideheaderBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("headeroverride"));
            //        }
            //        catch
            //        { };
            //        xtextbox.Text = xreader.ReadElementString("x-coordinate");
            //        ytextbox.Text = xreader.ReadElementString("y-coordinate");
            //        initscansbox.Text = xreader.ReadElementString("initscans");
            //        erodefactorbox.Text = xreader.ReadElementString("maxerodelimit");
            //        dxbox.Text = xreader.ReadElementString("cellsize");
            //        limitbox.Text = xreader.ReadElementString("memorylimit");
            //        minqbox.Text = xreader.ReadElementString("minq");
            //        creepratebox.Text = xreader.ReadElementString("creeprate");
            //        lateralratebox.Text = xreader.ReadElementString("lateralerosionrate");
            //        itermaxbox.Text = xreader.ReadElementString("maxiter");
            //        textBox1.Text = xreader.ReadElementString("runstarttime");
            //        cyclemaxbox.Text = xreader.ReadElementString("maxrunduration");
            //        slopebox.Text = xreader.ReadElementString("slopefailurethreshold");
            //        smoothbox.Text = xreader.ReadElementString("wssmoothingradius");
            //        mvaluebox.Text = xreader.ReadElementString("mvalue");

            //        grasstextbox.Text = xreader.ReadElementString("growgrasstime");
            //        textBox2.Text = xreader.ReadElementString("initialq");
            //        try
            //        {
            //            checkBox3.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("wssmoothing"));
            //        }
            //        catch
            //        { };
            //        grassbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("grass-sediment"));

            //        try // MJ 24/01/05
            //        {
            //            textBox3.Text = xreader.ReadElementString("flowdistribution");
            //            mintimestepbox.Text = xreader.ReadElementString("mintimestep");
            //        }
            //        catch
            //        { };

            //        try // MJ 15/03/05
            //        {
            //            k_evapBox.Text = xreader.ReadElementString("evaporation");
            //        }
            //        catch
            //        { };

            //        try // MJ 10/05/05
            //        {
            //            vegTauCritBox.Text = xreader.ReadElementString("vegcritshear");
            //        }
            //        catch
            //        { };

            //        try
            //        {
            //            bedslope_box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("bedslope"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            bool dum_bool = XmlConvert.ToBoolean(xreader.ReadElementString("wsslope"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            veltaubox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("veltaubox"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            catchment_mode_box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("catchment_mode"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            reach_mode_box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("reach_mode"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            latbox1.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("lat1"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            latbox2.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("lat2"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            bool dum_bool = XmlConvert.ToBoolean(xreader.ReadElementString("lat3"));
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            string dum_string = xreader.ReadElementString("cross_stream_grad");
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            max_vel_box.Text = xreader.ReadElementString("max_vel");
            //        }
            //        catch { };


            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem12.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem13.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem14.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem15.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem16.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem17.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem18.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem19.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem20.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem21.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem22.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem23.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem24.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("SaveOptions");
            //        dum = xreader.ReadElementString("Option");
            //        menuItem25.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //        xreader.ReadEndElement();
            //        try
            //        {
            //            xreader.ReadStartElement("SaveOptions");
            //            dum = xreader.ReadElementString("Option");
            //            menuItem29.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("SaveOptions");
            //            dum = xreader.ReadElementString("Option");
            //            menuItem33.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("SaveOptions");
            //            dum = xreader.ReadElementString("Option");
            //            menuItem34.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g1box.Text = xreader.ReadElementString("gs");
            //        gp1box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS1box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS1box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g2box.Text = xreader.ReadElementString("gs");
            //        gp2box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS2box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS2box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g3box.Text = xreader.ReadElementString("gs");
            //        gp3box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS3box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS3box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g4box.Text = xreader.ReadElementString("gs");
            //        gp4box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS4box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS4box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g5box.Text = xreader.ReadElementString("gs");
            //        gp5box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS5box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS5box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g6box.Text = xreader.ReadElementString("gs");
            //        gp6box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS6box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS6box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g7box.Text = xreader.ReadElementString("gs");
            //        gp7box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS7box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS7box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Grain-Size");
            //        g8box.Text = xreader.ReadElementString("gs");
            //        gp8box.Text = xreader.ReadElementString("gp");
            //        try
            //        {
            //            suspGS8box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //            fallGS8box.Text = xreader.ReadElementString("fv");
            //        }
            //        catch
            //        { };
            //        xreader.ReadEndElement();

            //        try
            //        {
            //            xreader.ReadStartElement("Grain-Size");
            //            g9box.Text = xreader.ReadElementString("gs");
            //            gp9box.Text = xreader.ReadElementString("gp");
            //            try
            //            {
            //                suspGS9box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
            //                fallGS9box.Text = xreader.ReadElementString("fv");
            //            }
            //            catch
            //            { };
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };

            //        xreader.ReadStartElement("File-Parms");

            //        input_time_step_box.Text = xreader.ReadElementString("inputtimestep");
            //        saveintervalbox.Text = xreader.ReadElementString("saveinterval");
            //        outputfilesaveintervalbox.Text = xreader.ReadElementString("savetologfileinterval");
            //        tracerbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("tracerrun"));
            //        uniquefilecheck.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("uniquefilecheck"));

            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        openfiletextbox.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        graindataloadbox.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        bedrockbox.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        raindataloadbox.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        tracerfile.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        tracerhydrofile.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        xreader.ReadStartElement("Filenames");
            //        dum = xreader.ReadElementString("Desc");
            //        tracergrainbox.Text = xreader.ReadElementString("Name");
            //        xreader.ReadEndElement();
            //        try
            //        {

            //            xreader.ReadStartElement("Sources");
            //            inbox1.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox1.Text = xreader.ReadElementString("X");
            //            ybox1.Text = xreader.ReadElementString("Y");
            //            infile1.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox2.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox2.Text = xreader.ReadElementString("X");
            //            ybox2.Text = xreader.ReadElementString("Y");
            //            infile2.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox3.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox3.Text = xreader.ReadElementString("X");
            //            ybox3.Text = xreader.ReadElementString("Y");
            //            infile3.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox4.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox4.Text = xreader.ReadElementString("X");
            //            ybox4.Text = xreader.ReadElementString("Y");
            //            infile4.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox5.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox5.Text = xreader.ReadElementString("X");
            //            ybox5.Text = xreader.ReadElementString("Y");
            //            infile5.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox6.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox6.Text = xreader.ReadElementString("X");
            //            ybox6.Text = xreader.ReadElementString("Y");
            //            infile6.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox7.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox7.Text = xreader.ReadElementString("X");
            //            ybox7.Text = xreader.ReadElementString("Y");
            //            infile7.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //            xreader.ReadStartElement("Sources");
            //            inbox8.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
            //            xbox8.Text = xreader.ReadElementString("X");
            //            ybox8.Text = xreader.ReadElementString("Y");
            //            infile8.Text = xreader.ReadElementString("Filename");
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };

            //        xreader.ReadEndElement();

            //        xreader.ReadStartElement("Description");
            //        DescBox.Text = xreader.ReadElementString("S");
            //        xreader.ReadEndElement();

            //        //JMW 2004-11-11
            //        try
            //        {
            //            xreader.ReadStartElement("OutputFile-Parms");
            //            checkBoxGenerateAVIFile.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("generateavifile"));
            //            textBoxAVIFile.Text = xreader.ReadElementString("avifile");
            //            try
            //            {
            //                saveintervalbox.Text = xreader.ReadElementString("avifreq");
            //                checkBoxGenerateTimeSeries.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("generatetimeseriesfile"));
            //                TimeseriesOutBox.Text = xreader.ReadElementString("timeseriesfile");
            //                outputfilesaveintervalbox.Text = xreader.ReadElementString("timeseriesfreq");
            //                checkBoxGenerateIterations.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("generateiterationsfile"));
            //                IterationOutbox.Text = xreader.ReadElementString("iterationsfile");
            //            }
            //            catch
            //            { };
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };

            //        try
            //        {
            //            xreader.ReadStartElement("Display");
            //            // have to have dumpvariable here as window not displayed yet...
            //            int dumpvarible = XmlConvert.ToInt16(xreader.ReadElementString("top"));
            //            dumpvarible = XmlConvert.ToInt16(xreader.ReadElementString("left"));
            //            dumpvarible = XmlConvert.ToInt16(xreader.ReadElementString("width"));
            //            dumpvarible = XmlConvert.ToInt16(xreader.ReadElementString("height"));
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };

            //        try
            //        {
            //            xreader.ReadStartElement("Lateral");
            //            bool dum_bool = XmlConvert.ToBoolean(xreader.ReadElementString("oldlat"));
            //            newlateral.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("newlat"));
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };
            //        try
            //        {
            //            xreader.ReadStartElement("Add_Ons");
            //            tracerOutcheckBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("tracer-out"));
            //            tracerOutputtextBox.Text = xreader.ReadElementString("tracer-out-filename");
            //            googleAnimationCheckbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("google_animation"));
            //            googleAnimationTextBox.Text = xreader.ReadElementString("google_animation_file_name");
            //            googleBeginDate.Text = xreader.ReadElementString("google_begin");
            //            googAnimationSaveInterval.Text = xreader.ReadElementString("google_interval");
            //            jmeaninputfilebox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("jMean"));
            //            avge_smoothbox.Text = xreader.ReadElementString("edge_smoothing");
            //            string dum_string = xreader.ReadElementString("displacement");
            //            propremaining.Text = xreader.ReadElementString("prop_remain");
            //            max_time_step_Box.Text = xreader.ReadElementString("max_time_step");
            //            mine_checkBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("contam_input"));
            //            mineX_textBox.Text = xreader.ReadElementString("mineX");
            //            mineY_textBox.Text = xreader.ReadElementString("mineY");
            //            mine_input_textBox.Text = xreader.ReadElementString("contam_input_file");
            //            soil_ratebox.Text = xreader.ReadElementString("soil_rate");
            //            SiberiaBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("siberia"));
            //            Beta1Box.Text = xreader.ReadElementString("beta1");
            //            Beta3Box.Text = xreader.ReadElementString("beta3");
            //            m1Box.Text = xreader.ReadElementString("m1");
            //            m3Box.Text = xreader.ReadElementString("m3");
            //            n1Box.Text = xreader.ReadElementString("n1");
            //            Q2box.Text = xreader.ReadElementString("W_depth_erosion_threshold");
            //            dum_string = xreader.ReadElementString("fexp");
            //            div_inputs_box.Text = xreader.ReadElementString("div_inputs");

            //            init_depth_box.Text = xreader.ReadElementString("initial_sand_depth");
            //            slab_depth_box.Text = xreader.ReadElementString("maxslabdepth");
            //            shadow_angle_box.Text = xreader.ReadElementString("angle");
            //            upstream_check_box.Text = xreader.ReadElementString("checkup");
            //            depo_prob_box.Text = xreader.ReadElementString("dep_probability");
            //            offset_box.Text = xreader.ReadElementString("downstream_offset");
            //            dune_time_box.Text = xreader.ReadElementString("dune_timestep");
            //            dune_grid_size_box.Text = xreader.ReadElementString("dune_gridsize");

            //            wilcockbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("wilcock"));
            //            einsteinbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("einstein"));
            //            DuneBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("dune"));

            //            UTMgridcheckbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("UTM"));
            //            UTMsouthcheck.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("South"));
            //            UTMzonebox.Text = xreader.ReadElementString("UTMzone");

            //            raintimestepbox.Text = xreader.ReadElementString("raindatatimestep");
            //            activebox.Text = xreader.ReadElementString("activelayerthickness");

            //            xreader.ReadEndElement();
            //            xreader.ReadEndElement();
            //        }
            //        catch
            //        { };

            //        xreader.Close();


            //        this.Text = basetext + " (" + Path.GetFileName(cfgname) + ")";
            //        button2.Enabled = true;
            //        start_button.Enabled = false;
            //        Panel1.Visible = false;
            //        tabControl1.Visible = true;

            //    }
            //}

            //////// then initialise

            //int ok;
            //ok = read_header();

            //if (ok == 1)
            //{
            //    initialise();
            //    zero_values();
            //    load_data();

            //    // nActualGridSize
            //    // moved from initialse() to here MJ 29/03/05
            //    int x, y;
            //    nActualGridCells = 0;
            //    for (x = 1; x <= xmax; x++)
            //    {
            //        for (y = 1; y <= ymax; y++)
            //        {
            //            if (elev[x, y] > -9999) nActualGridCells++;
            //        }
            //    }

            //    tabControl1.Visible = false;
            //    checkBox1.Checked = false;
            //    zoomPanImageBox1.Visible = true;// MJ 14/01/05
            //    Panel1.Visible = true;						// MJ 14/01/05
            //    button2.Enabled = false;					// MJ 17/01/05
            //    start_button.Enabled = true;				// MJ 17/01/05
            //    groupBox2.Visible = true;
            //    groupBox3.Visible = true;
            //}

            //// then run program

            //main_loop(this, null);

            ////
            //// end of batch mode section
            ////
            ////
        }

        void button1_Click(object sender, System.EventArgs e)
        {
            //close google earth animation kml and make kmz
            if (googleAnimationCheckbox.Checked == true)
            {
                StreamWriter kmlsr = File.AppendText(KML_FILE_NAME);
                kml = "\n</Folder>"
                      + "\n</kml>";
                kmlsr.WriteLine(kml);
                kmlsr.Close();
            }

            if (menuItem25.Checked == true) save_data(1, 0); // save waterdepths
            if (menuItem13.Checked == true) save_data(2, 0); // save elevdiff
            if (menuItem12.Checked == true) save_data(3, 0); // save elevations
            if (menuItem14.Checked == true) save_data(4, 0); // save grainsize
            if (menuItem29.Checked == true) save_data(15, 0); // save d50 top layer
            if (menuItem33.Checked == true) save_data(16, 0); // save velocity	<JOE 20050605>
            if (menuItem34.Checked == true) save_data(17, 0); // save soil_saturation	<JOE 20050605>
            if (menuItem6.Checked == true) save_data(18, 0); // save water tracers - MDW 17-03-2016
            if (menuItem15.Checked == true) save_data(19, 0); // save rain zone tracers - MDW 13-04-2016
            if (menuItem16.Checked == true) save_data(6, 0);  // save tracer file <Jun 20200527>
            if (menuItemSoluteTracer.Checked == true) save_data(20, 0);  // save solute tracers - MDW_V2
            if (menuItemOilSpill.Checked == true) save_data(7, 0);  // save oildepth file OIL_V1_PDF
            if (menuItem17.Checked == true) save_data(8, 0);   // save massbalance file OIL_V1_PDF
            if (menuItemOilconc.Checked == true) save_data(9, 0);   // save oilconcentration file OIL_V1_PDF

            this.Close();
        }
        private void button2_Click(object sender, System.EventArgs e)
        {
            int ok;
            ok = read_header();
            int nnn;
            double temp = -9999;

            if (ok == 1)
            {
                //sourceIDs = new int[10]; // MDW_V2 - commented as array now expands as needed

                initialise();
                zero_values();
                load_data();

                // TEMP_V1 - dhdt_x/dhdt_y are needed by depth_update() and
                // update_water_temperature_advection() whenever temperature simulation is
                // active, even if water-source tracing itself is switched off. water_depth_prev
                // is allocated here too for consistency, though save_temperature_states()
                // itself only needs water_temp_prev (already allocated unconditionally, Step 2).
                // Allocate here if the isTraceWater block below won't be doing it.
                if (isSimulateTemperature == true && isTraceWater == false)
                {
                    water_depth_prev = new double[xmax + 2, ymax + 2];
                    dhdt_x = new double[xmax + 2, ymax + 2];
                    dhdt_y = new double[xmax + 2, ymax + 2];
                }

                // TEMP_V1 - nSources is needed for source-temperature file column mapping
                // even when water-source tracing itself is disabled.
                if (isSimulateTemperature == true && isTraceWater == false)
                {
                    if (sourceIDs.Length == 0) { nSources = sourceIndexAddition; }
                    else { nSources = sourceIDs.Distinct().Count() + sourceIndexAddition; }
                }

                // Additional initialisation for water source tracing - MDW 13/03/16
                if (isTraceWater == true)
                {
                    //MDW_V2 : enable the water source GUI controls (disabled by default)
                    this.label110.Enabled = true; // text label for water source tracing visibility
                    this.label109.Enabled = true; // Red, label R
                    this.label106.Enabled = true; // Green, label G
                    this.label107.Enabled = true; // Blue, label B
                    this.label108.Enabled = true; // Enhance text label
                    this.comboBox4.Enabled = true; // Blue drop down box
                    this.comboBox3.Enabled = true; // Green drop down box
                    this.comboBox2.Enabled = true; // Red drop down box
                    this.trackBar3.Enabled = true; // enhance slider tracker
                    this.groupBox9.Enabled = true; // box around water source controls above
                                                   //end


                    //nSources = number_of_points + 2;
                    //if (sourceIDs.Length == 0) { nSources = sourceIndexAddition; } // MDW_V2 logic added as sourceIDs is now initialised at length zero
                    //else { nSources = sourceIDs.Length + sourceIndexAddition; } //sourceIDs.Max(); } // sources 1 and 2 are rainfall and stage inputs. The rest are hydrograph inputs.

                    //nSources = number_of_points + 2;

                    if (sourceIDs.Length == 0) { nSources = sourceIndexAddition; } // MDW_V2 logic added as sourceIDs is now initialised at length zero

                    else
                    {

                        nSources = sourceIDs.Distinct().Count() + sourceIndexAddition; // correction of bug where the same file used for multiple points was counted for each point

                        //nSources = sourceIDs.Length + sourceIndexAddition;

                    } //sourceIDs.Max(); } // sources 1 and 2 are rainfall and stage inputs. The rest are hydrograph inputs.

                    water_depth_prev = new double[xmax + 2, ymax + 2];
                    watertracer = new double[xmax + 2, ymax + 2, nSources]; // MDW_V2 - no need for the additional empty layer
                    watertracer_prev = new double[xmax + 2, ymax + 2, nSources]; // MDW_V2
                    dhdt_x = new double[xmax + 2, ymax + 2];
                    dhdt_y = new double[xmax + 2, ymax + 2];
                    trace_rgb = new int[3];
                    if (isTraceRainZonation == true)
                    {
                        this.checkBox12.Enabled = true; // MDW_V2 : Rain zones checkbox
                        watertracerRainZone = new double[xmax + 2, ymax + 2, nRainZones]; // MDW_V2 - no need for the additional empty layer
                        watertracerRainZone_prev = new double[xmax + 2, ymax + 2, nRainZones]; // MDW_V2
                        tracerain_rgb = new int[3];
                        tracerain_rgb[0] = -1; // set for initialisation
                    }
                    if (isTraceSolutes == true) // MDW_V2
                    {
                        this.checkBoxSoluteVis.Enabled = true;
                        solutetracer = new double[xmax + 2, ymax + 2, nSolutes];
                        solutetracer_prev = new double[xmax + 2, ymax + 2, nSolutes];
                        tracesolute_rgb = new int[3];
                        tracesolute_rgb[0] = -1;
                    }

                    // Add water sources to graphics control and assign default values: R=1, G=2, B=3
                    comboBox2.Items.Add("0 : [none]"); // MDW_V2
                    comboBox3.Items.Add("0 : [none]"); // MDW_V2
                    comboBox4.Items.Add("0 : [none]"); // MDW_V2

                    for (int z = 0; z < nSources; z++) // MDW_V2 updated to zero index
                    {
                        // MDW_V2 - source selector box string generation, updated indices
                        string comboText = "";
                        if (z == 0) // tide source
                        {
                            if (checkBox3.Checked == true)
                            {
                                comboText = Convert.ToString(z + 1) + " : Stage";
                            }
                            else
                            {
                                comboText = Convert.ToString(z + 1) + " : Stage [not active]";
                            }
                        }
                        else if (z == 1) // rain source
                        {
                            if (catchment_mode_box.Checked == true)
                            {
                                comboText = Convert.ToString(z + 1) + " : Rain";
                            }
                            else
                            {
                                comboText = Convert.ToString(z + 1) + " : Rain [not active]";
                            }
                        }
                        else if (z >= 2) // hydro sources
                        {
                            comboText = Convert.ToString(z + 1) + " : " + inputfilenames[z - sourceIndexAddition];
                        }
                        comboBox2.Items.Add(comboText);
                        comboBox3.Items.Add(comboText);
                        comboBox4.Items.Add(comboText);
                    }
                    // MDW_V2 updated default selections
                    if (nSources >= 3) { comboBox2.SelectedIndex = 3; trace_rgb[0] = 3; }
                    else { comboBox2.SelectedIndex = 0; trace_rgb[0] = 0; }
                    if (nSources >= 4) { comboBox3.SelectedIndex = 4; trace_rgb[1] = 4; }
                    else { comboBox3.SelectedIndex = 1; trace_rgb[1] = 1; }
                    if (nSources >= 5) { comboBox4.SelectedIndex = 5; trace_rgb[2] = 5; }
                    else { comboBox4.SelectedIndex = 0; trace_rgb[2] = 0; }



                }

                // OIL_V1
                if (isOilSimulation == true) initialise_oil_simulation();

                // TEMP_V1 - source temperature file, loaded after nSources is finalised above
                if (isSimulateTemperature == true)
                {
                    load_source_temp_file(this.TempTab_textBox_sourcetemp.Text, new char[] { ' ', ',', '\t' });
                }

                // nActualGridSize
                // moved from initialse() to here MJ 29/03/05
                int x, y;
                //nActualGridCells = 0;
                for (int ii = 1; ii <= rfnum; ii++) nActualGridCells[ii] = 0;

                for (x = 1; x <= xmax; x++)
                {
                    for (y = 1; y <= ymax; y++)
                    {
                        if (elev[x, y] > -9999) nActualGridCells[rfarea[x, y]]++;
                        if (tracer_area[x, y] == 1) addGS(x, y);
                    }
                }



                tabControl1.Visible = false;
                checkBox1.Checked = false;
                zoomPanImageBox1.Visible = true;// MJ 14/01/05
                Panel1.Visible = true;                      // MJ 14/01/05
                button2.Enabled = false;                    // MJ 17/01/05
                start_button.Enabled = true;                // MJ 17/01/05
                groupBox2.Visible = true;
                groupBox3.Visible = true;
            }

            string message = "Variables check:";
            if ((xmax * ymax) > 250000) message += "\n\nWarning, number of cells is greater than 250 000 - this may result in slow model operation";
            if (MIN_Q < (DX / 120)) message += "\n\nWarning, Min_Q may be set too low - suitable value is normally cell size / 100";
            if (MIN_Q > (DX / 80)) message += "\n\nWarning, Min_Q may be set too high - suitable value is normally cell size / 100";
            if (reach_mode_box.Checked == true && inbox1.Checked == false) message += "\n\nWarning, model set to run in reach mode, but no point inputs selected (Hydrology tab)";
            if (reach_mode_box.Checked == true && div_inputs < 0) message += "\n\nWarning, model set to run in reach mode, but divide inputs box (Hydrology tab) set to 0\nit must be 1 or greater";
            if (max_time_step > 3600)
            {
                message += "\n\nMax time step (numerical tab) is set to greater than 3600 - if running in catchment mode\nthis must be smaller than 3600";

            }
            if (water_depth_erosion_threshold > 0.02)
            {
                message += "\n\nWarning, Min depth for erosion threshold (numerical tab) may be set too high";
                message += "\nthis could result in erosion not happening in cells where water depths are low try a value of 0.02 or lower";
            }
            if (water_depth_erosion_threshold < 0.005)
            {
                message += "\n\nWarning, Min depth for erosion threshold (numerical tab) may be set too low";
                message += "\nthis may lead to slow operation as the model tries to erode where very shallow depths ";
            }
            if (d1 > d2 || d2 > d3 || d3 > d4) message += "\n\nWarning, sediment sizes (sediment tab) must be entered in ASCENDING order of size";
            //if (M > 0.1 || M < 0.001) message += "\n\nWarning, M value is unusually high or low. Typical values range from 0.005 to 0.02";

            //check for -9999's on RH edge of DEM
            for (nnn = 1; nnn <= ymax; nnn++)
            {
                if (elev[xmax, nnn] > temp) temp = elev[xmax, nnn];
            }
            if (temp < -10)
            {
                message += "\n\nDEM ERROR: CAESAR will not function properly, as the right hand column of the DEM is all nodata (-9999) values. This will prevent any water or sediment from leaving the Rh edge of the model/dem";
            }
            if (edgeslope > 0.01) message += "\nThe edge slope (slope at exit cells for hydraulic model) is probably set too high.. normal values are 0.01 to 0.001";
            //if (bed_proportion > 0.05) message += "\nThe proportion of bedslope erosion is set high - please check";
            //if (bed_proportion > 1) message += "\nProportion of bedlsope erosion is greater than 1 - this must be reduced or the model will not function correctly";
            if (courant_number > 0.7) message += "\nThe courant number is set too high, numerical instabilities are highly likely, it is best set to < 0.5";
            if (courant_number >= 0.4 && DX <= 25) message += "\nThe courant number may be set a little to high for this resolution - consider changing to below 0.4";
            if (courant_number >= 0.3 && DX <= 10) message += "\nThe courant number may be set a little to high for this resolution - consider changing to below 0.3";
            //if (min_time_step <= 0) message += "\nConsider using a minimum time step (e.g. 1 sec or greater) as low time steps can lead to excessive scour during the first few min of model operation";
            if (in_out_difference != 0) message += "\n\nYou have set the input/output difference to be greater than zero, which means the model will speed up/run in steady state  when the difference between water input and output is less than this value";

            message += "\n\nAll other variables are OK";
            MessageBox.Show(message);

            //main_loop(this, null);
            simLoadState = true; // MDW: added to signify that data are loaded, allowing a fix to an exception which happens when selecting an item from the graphics menu before data are loaded

        }
        private void buttonOutDir_Click(object sender, System.EventArgs e) // MDW_V2
        {
            // Show the FolderBrowserDialog to select output directory
            DialogResult result = folderBrowserOutDir.ShowDialog();
            if (result == DialogResult.OK)
            {
                textBoxOutDir.Text = folderBrowserOutDir.SelectedPath;
            }
        }
        private void textBoxOutDir_TextChanged(object sender, System.EventArgs e) // MDW_V2
        {
            //outDirCheck();
        }
        private void checkboxOutDirDateTime_CheckChanged(object sender, System.EventArgs e) // MDW_V2
        {
            outdirDateTime = checkboxOutDirDateTime.Checked;
        }
        private void outDirCheck()
        {
            try
            {
                outdir = textBoxOutDir.Text;
                outdirDateTime = checkboxOutDirDateTime.Checked;

                if (string.IsNullOrEmpty(outdir) == true && outdirDateTime == false)
                {
                    outdir = "";
                    return;
                }

                if (string.IsNullOrEmpty(outdir) == false && Directory.Exists(outdir) == false)
                {
                    Directory.CreateDirectory(outdir);
                }

                if (outdirDateTime == true)
                {
                    DateTime currentDateTime = DateTime.Now;
                    string formattedDateTime = currentDateTime.ToString("yyyy-MM-ddTHH-mm-ss");
                    outdir = Path.Combine(outdir, formattedDateTime);
                    Directory.CreateDirectory(outdir);
                }

                //MessageBox.Show("Output directory set to : " + outdir);
            }
            catch
            {
                MessageBox.Show("Error with output folder : " + textBoxOutDir.Text + "/nFalling back to using current directory for outputs");
                outdir = "";
            }

        }
        private void textBox2_TextChanged(object sender, System.EventArgs e)
        {

        }
        private void contextMenu1_Popup(object sender, System.EventArgs e)
        {

        }
        private void popComboBox1()
        {
            if (comboBox1.Items.Count == 1)
            {
                comboBox1.Text = "water depth";
                comboBox1.Text = "erosion/dep";
                comboBox1.Text = "Bed sheer stress";
                comboBox1.Text = "grainsize";
                comboBox1.Text = "tracer";
                comboBox1.Text = "susp conc";
                comboBox1.Text = "soil depth";
                comboBox1.Text = "flow velocity";
            }
        }
        private void menuItem3_Click(object sender, System.EventArgs e)
        {
            menuItem3.Checked = (!menuItem3.Checked);
            if (menuItem3.Checked == true)
            {
                comboBox1.Items.Add("water depth");
            }
            else
            {
                comboBox1.Items.Remove("water depth");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);

        }
        private void menuItem4_Click(object sender, System.EventArgs e)
        {
            menuItem4.Checked = (!menuItem4.Checked);
            if (menuItem4.Checked == true)
            {
                comboBox1.Items.Add("erosion/dep");
            }
            else
            {
                comboBox1.Items.Remove("erosion/dep");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem5_Click(object sender, System.EventArgs e)
        {
            menuItem5.Checked = (!menuItem5.Checked);
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem7_Click(object sender, System.EventArgs e)
        {
            menuItem7.Checked = (!menuItem7.Checked);
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem8_Click(object sender, System.EventArgs e)
        {
            menuItem8.Checked = (!menuItem8.Checked);
            if (menuItem8.Checked == true)
            {
                comboBox1.Items.Add("Bed sheer stress");
            }
            else
            {
                comboBox1.Items.Remove("Bed sheer stress");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem9_Click(object sender, System.EventArgs e)
        {
            menuItem9.Checked = (!menuItem9.Checked);
            if (menuItem9.Checked == true)
            {
                comboBox1.Items.Add("grainsize");
            }
            else
            {
                comboBox1.Items.Remove("grainsize");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem12_Click(object sender, System.EventArgs e)
        {
            menuItem12.Checked = (!menuItem12.Checked);
        }
        private void menuItem13_Click(object sender, System.EventArgs e)
        {
            menuItem13.Checked = (!menuItem13.Checked);
        }
        private void menuItem14_Click(object sender, System.EventArgs e)
        {
            menuItem14.Checked = (!menuItem14.Checked);
        }
        private void menuItem25_Click(object sender, System.EventArgs e)
        {
            menuItem25.Checked = (!menuItem25.Checked);
        }
        private void menuItem26_Click(object sender, System.EventArgs e)
        {
            menuItem26.Checked = (!menuItem26.Checked);
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem27_Click(object sender, System.EventArgs e)
        {
            menuItem27.Checked = (!menuItem27.Checked);
            if (menuItem27.Checked == true)
            {
                comboBox1.Items.Add("susp conc");
            }
            else
            {
                comboBox1.Items.Remove("susp conc");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem28_Click(object sender, System.EventArgs e)
        {
            menuItem28.Checked = (!menuItem28.Checked);
            if (menuItem28.Checked == true)
            {
                comboBox1.Items.Add("soil depth");
            }
            else
            {
                comboBox1.Items.Remove("soil depth");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem29_Click(object sender, System.EventArgs e)
        {
            menuItem29.Checked = (!menuItem29.Checked);
        }
        private void menuItem30_Click(object sender, System.EventArgs e)
        {
            menuItem30.Checked = (!menuItem30.Checked);
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem31_Click(object sender, System.EventArgs e)
        {
            menuItem31.Checked = (!menuItem31.Checked);
            if (menuItem31.Checked == true)
            {
                comboBox1.Items.Add("flow velocity");
            }
            else
            {
                comboBox1.Items.Remove("flow velocity");
            }
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void menuItem33_Click(object sender, System.EventArgs e)
        {
            menuItem33.Checked = (!menuItem33.Checked);
        }
        private void menuItem34_Click(object sender, System.EventArgs e)
        {
            menuItem34.Checked = (!menuItem34.Checked);
        }
        private void menuItemSoluteTracer_Click(object sender, System.EventArgs e) // MDW_V2
        {
            menuItemSoluteTracer.Checked = (!menuItemSoluteTracer.Checked);
        }
        private void menuItemOilSpill_Click(object sender, System.EventArgs e)
        {
            menuItemOilSpill.Checked = (!menuItemOilSpill.Checked);// OIL_V1_PDF
        }
        private void menuItemOilconc_Click(object sender, System.EventArgs e)
        {
            menuItemOilconc.Checked = (!menuItemOilconc.Checked);
        }
        private void menuItem17_Click(object sender, System.EventArgs e)
        {
            menuItem17.Checked = (!menuItem17.Checked);
        }
        private void menuItem6_Click(object sender, System.EventArgs e) // MDW
        {
            menuItem6.Checked = (!menuItem6.Checked);
        }
        private void menuItem15_Click(object sender, System.EventArgs e) // MDW
        {
            menuItem15.Checked = (!menuItem15.Checked);
        }
        private void button3_Click(object sender, System.EventArgs e)
        {
            grow_grass(1);
        }
        private void menuItemConfigFileOpen_Click(object sender, System.EventArgs e)
        {
            XmlTextReader xreader;
            String dum;

            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = workdir;
            openFileDialog1.Filter = "cfg files (*.xml)|*.xml|All files (*.*)|*.*";
            openFileDialog1.FilterIndex = 1;
            openFileDialog1.RestoreDirectory = false;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                cfgname = openFileDialog1.FileName;

                xreader = new XmlTextReader(cfgname);

                //Read the file
                if (xreader != null)
                {
                    xreader.ReadStartElement("Parms");
                    xreader.ReadStartElement("General-Parms");
                    try
                    {
                        overrideheaderBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("headeroverride"));
                    }
                    catch
                    { }
                    ;
                    xtextbox.Text = xreader.ReadElementString("x-coordinate");
                    ytextbox.Text = xreader.ReadElementString("y-coordinate");
                    initscansbox.Text = xreader.ReadElementString("initscans");
                    erodefactorbox.Text = xreader.ReadElementString("maxerodelimit");
                    dxbox.Text = xreader.ReadElementString("cellsize");
                    limitbox.Text = xreader.ReadElementString("memorylimit");
                    minqbox.Text = xreader.ReadElementString("minq");
                    creepratebox.Text = xreader.ReadElementString("creeprate");
                    lateralratebox.Text = xreader.ReadElementString("lateralerosionrate");
                    itermaxbox.Text = xreader.ReadElementString("maxiter");
                    textBox1.Text = xreader.ReadElementString("runstarttime");
                    cyclemaxbox.Text = xreader.ReadElementString("maxrunduration");
                    slopebox.Text = xreader.ReadElementString("slopefailurethreshold");
                    smoothbox.Text = xreader.ReadElementString("wssmoothingradius");
                    mvaluebox.Text = xreader.ReadElementString("mvalue");

                    grasstextbox.Text = xreader.ReadElementString("growgrasstime");
                    textBox2.Text = xreader.ReadElementString("initialq");
                    try
                    {
                        bool dummy6 = XmlConvert.ToBoolean(xreader.ReadElementString("wssmoothing"));
                    }
                    catch
                    { }
                    ;
                    flowonlybox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("grass-sediment"));

                    try // MJ 24/01/05
                    {
                        textBox3.Text = xreader.ReadElementString("flowdistribution");
                        mintimestepbox.Text = xreader.ReadElementString("mintimestep");
                    }
                    catch
                    { }
                    ;

                    try // MJ 15/03/05
                    {
                        k_evapBox.Text = xreader.ReadElementString("evaporation");
                    }
                    catch
                    { }
                    ;

                    try // MJ 10/05/05
                    {
                        vegTauCritBox.Text = xreader.ReadElementString("vegcritshear");
                    }
                    catch
                    { }
                    ;

                    try
                    {
                        bedslope_box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("bedslope"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        bool dum_bool = XmlConvert.ToBoolean(xreader.ReadElementString("wsslope"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        veltaubox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("veltaubox"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        catchment_mode_box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("catchment_mode"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        reach_mode_box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("reach_mode"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        bool dum_bool2 = XmlConvert.ToBoolean(xreader.ReadElementString("lat1"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        bool dum_bool2 = XmlConvert.ToBoolean(xreader.ReadElementString("lat2"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        bool dum_bool = XmlConvert.ToBoolean(xreader.ReadElementString("lat3"));
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        string dum_string = xreader.ReadElementString("cross_stream_grad");
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        max_vel_box.Text = xreader.ReadElementString("max_vel");
                    }
                    catch { }
                    ;


                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    menuItem12.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    menuItem13.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    menuItem14.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    menuItem6.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    bool dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    dummy4 = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");
                    dum = xreader.ReadElementString("Option");
                    menuItem25.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions");  // MDW_V2
                    dum = xreader.ReadElementString("Option");
                    menuItemSoluteTracer.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    xreader.ReadStartElement("SaveOptions"); // OIL_V1_PDF
                    dum = xreader.ReadElementString("Option");
                    menuItemOilSpill.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();
                    /*xreader.ReadStartElement("SaveOptions"); // OIL_V1_PDF
                    dum = xreader.ReadElementString("Option");
                    menuItem17.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                    xreader.ReadEndElement();*/
                    try
                    {
                        xreader.ReadStartElement("SaveOptions");
                        dum = xreader.ReadElementString("Option");
                        menuItem29.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("SaveOptions");
                        dum = xreader.ReadElementString("Option");
                        menuItem33.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("SaveOptions");
                        dum = xreader.ReadElementString("Option");
                        menuItem34.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g1box.Text = xreader.ReadElementString("gs");
                    gp1box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS1box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS1box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g2box.Text = xreader.ReadElementString("gs");
                    gp2box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS2box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS2box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g3box.Text = xreader.ReadElementString("gs");
                    gp3box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS3box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS3box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g4box.Text = xreader.ReadElementString("gs");
                    gp4box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS4box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS4box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g5box.Text = xreader.ReadElementString("gs");
                    gp5box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS5box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS5box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g6box.Text = xreader.ReadElementString("gs");
                    gp6box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS6box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS6box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g7box.Text = xreader.ReadElementString("gs");
                    gp7box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS7box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS7box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Grain-Size");
                    g8box.Text = xreader.ReadElementString("gs");
                    gp8box.Text = xreader.ReadElementString("gp");
                    try
                    {
                        suspGS8box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                        fallGS8box.Text = xreader.ReadElementString("fv");
                    }
                    catch
                    { }
                    ;
                    xreader.ReadEndElement();

                    try
                    {
                        xreader.ReadStartElement("Grain-Size");
                        g9box.Text = xreader.ReadElementString("gs");
                        gp9box.Text = xreader.ReadElementString("gp");
                        try
                        {
                            suspGS9box.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("ss"));
                            fallGS9box.Text = xreader.ReadElementString("fv");
                        }
                        catch
                        { }
                        ;
                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;

                    try
                    {
                        xreader.ReadStartElement("Grain-Size");
                        g1_box.Text = xreader.ReadElementString("gs");
                        gp1_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g2_box.Text = xreader.ReadElementString("gs");
                        gp2_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g3_box.Text = xreader.ReadElementString("gs");
                        gp3_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g4_box.Text = xreader.ReadElementString("gs");
                        gp4_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g5_box.Text = xreader.ReadElementString("gs");
                        gp5_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g6_box.Text = xreader.ReadElementString("gs");
                        gp6_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g7_box.Text = xreader.ReadElementString("gs");
                        gp7_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g8_box.Text = xreader.ReadElementString("gs");
                        gp8_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Grain-Size");
                        g9_box.Text = xreader.ReadElementString("gs");
                        gp9_box.Text = xreader.ReadElementString("gp");
                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;

                    try
                    {
                        xreader.ReadStartElement("File-Parms");

                        input_time_step_box.Text = xreader.ReadElementString("inputtimestep");
                        saveintervalbox.Text = xreader.ReadElementString("saveinterval");
                        outputfilesaveintervalbox.Text = xreader.ReadElementString("savetologfileinterval");
                        tracerbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("tracerrun"));
                        uniquefilecheck.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("uniquefilecheck"));

                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        openfiletextbox.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        graindataloadbox.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        bedrockbox.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();

                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        raindataloadbox.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        mine_input_textBox.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        tracerhydrofile.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        string dummystring4 = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;

                    try
                    {

                        xreader.ReadStartElement("Sources");
                        inbox1.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox1.Text = xreader.ReadElementString("X");
                        ybox1.Text = xreader.ReadElementString("Y");
                        infile1.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox2.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox2.Text = xreader.ReadElementString("X");
                        ybox2.Text = xreader.ReadElementString("Y");
                        infile2.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox3.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox3.Text = xreader.ReadElementString("X");
                        ybox3.Text = xreader.ReadElementString("Y");
                        infile3.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox4.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox4.Text = xreader.ReadElementString("X");
                        ybox4.Text = xreader.ReadElementString("Y");
                        infile4.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox5.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox5.Text = xreader.ReadElementString("X");
                        ybox5.Text = xreader.ReadElementString("Y");
                        infile5.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox6.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox6.Text = xreader.ReadElementString("X");
                        ybox6.Text = xreader.ReadElementString("Y");
                        infile6.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox7.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox7.Text = xreader.ReadElementString("X");
                        ybox7.Text = xreader.ReadElementString("Y");
                        infile7.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Sources");
                        inbox8.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));
                        xbox8.Text = xreader.ReadElementString("X");
                        ybox8.Text = xreader.ReadElementString("Y");
                        infile8.Text = xreader.ReadElementString("Filename");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("ExtraSources");                                       //MDW_V2
                        inboxExtra.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("input"));  //
                        infileExtra.Text = xreader.ReadElementString("Filename");                       //
                        xreader.ReadEndElement();                                                       //

                    }
                    catch
                    { }
                    ;

                    xreader.ReadEndElement();

                    xreader.ReadStartElement("Description");
                    DescBox.Text = xreader.ReadElementString("S");
                    xreader.ReadEndElement();

                    //JMW 2004-11-11
                    try
                    {
                        xreader.ReadStartElement("OutputFile-Parms");
                        try
                        {
                            bool a124 = XmlConvert.ToBoolean(xreader.ReadElementString("generateavifile"));
                            string a123 = xreader.ReadElementString("avifile");

                        }
                        catch { }
                        ;
                        try
                        {
                            saveintervalbox.Text = xreader.ReadElementString("avifreq");
                            checkBoxGenerateTimeSeries.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("generatetimeseriesfile"));
                            TimeseriesOutBox.Text = xreader.ReadElementString("timeseriesfile");
                            outputfilesaveintervalbox.Text = xreader.ReadElementString("timeseriesfreq");
                            checkBoxGenerateIterations.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("generateiterationsfile"));
                            IterationOutbox.Text = xreader.ReadElementString("iterationsfile");
                        }
                        catch
                        { }
                        ;
                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;

                    try
                    {
                        xreader.ReadStartElement("Display");
                        Form1.ActiveForm.Top = XmlConvert.ToInt16(xreader.ReadElementString("top"));
                        Form1.ActiveForm.Left = XmlConvert.ToInt16(xreader.ReadElementString("left"));
                        Form1.ActiveForm.Width = XmlConvert.ToInt16(xreader.ReadElementString("width"));
                        Form1.ActiveForm.Height = XmlConvert.ToInt16(xreader.ReadElementString("height"));

                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;

                    try
                    {
                        xreader.ReadStartElement("Lateral");
                        bool dum_bool = XmlConvert.ToBoolean(xreader.ReadElementString("oldlat"));
                        newlateral.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("newlat"));
                        xreader.ReadEndElement();
                    }
                    catch
                    { }
                    ;
                    try
                    {
                        xreader.ReadStartElement("Add_Ons");
                        tracerOutcheckBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("tracer-out"));
                        tracerOutputtextBox.Text = xreader.ReadElementString("tracer-out-filename");
                        googleAnimationCheckbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("google_animation"));
                        googleAnimationTextBox.Text = xreader.ReadElementString("google_animation_file_name");
                        googleBeginDate.Text = xreader.ReadElementString("google_begin");
                        googAnimationSaveInterval.Text = xreader.ReadElementString("google_interval");
                        jmeaninputfilebox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("jMean"));
                        avge_smoothbox.Text = xreader.ReadElementString("edge_smoothing");
                        string dum_string = xreader.ReadElementString("displacement");
                        propremaining.Text = xreader.ReadElementString("prop_remain");
                        max_time_step_Box.Text = xreader.ReadElementString("max_time_step");
                        bool dummy1 = XmlConvert.ToBoolean(xreader.ReadElementString("contam_input"));
                        string dummystring1 = xreader.ReadElementString("mineX");
                        string dummystring2 = xreader.ReadElementString("mineY");
                        string dummystring3 = xreader.ReadElementString("contam_input_file");
                        soil_ratebox.Text = xreader.ReadElementString("soil_rate");
                        SiberiaBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("siberia"));
                        Beta1Box.Text = xreader.ReadElementString("beta1");
                        Beta3Box.Text = xreader.ReadElementString("beta3");
                        m1Box.Text = xreader.ReadElementString("m1");
                        m3Box.Text = xreader.ReadElementString("m3");
                        n1Box.Text = xreader.ReadElementString("n1");
                        Q2box.Text = xreader.ReadElementString("W_depth_erosion_threshold");
                        dum_string = xreader.ReadElementString("fexp");
                        div_inputs_box.Text = xreader.ReadElementString("div_inputs");

                        init_depth_box.Text = xreader.ReadElementString("initial_sand_depth");
                        slab_depth_box.Text = xreader.ReadElementString("maxslabdepth");
                        shadow_angle_box.Text = xreader.ReadElementString("angle");
                        upstream_check_box.Text = xreader.ReadElementString("checkup");
                        depo_prob_box.Text = xreader.ReadElementString("dep_probability");
                        offset_box.Text = xreader.ReadElementString("downstream_offset");
                        dune_time_box.Text = xreader.ReadElementString("dune_timestep");
                        dune_grid_size_box.Text = xreader.ReadElementString("dune_gridsize");

                        wilcockbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("wilcock"));
                        einsteinbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("einstein"));
                        DuneBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("dune"));

                        UTMgridcheckbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("UTM"));
                        UTMsouthcheck.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("South"));
                        UTMzonebox.Text = xreader.ReadElementString("UTMzone");


                        raintimestepbox.Text = xreader.ReadElementString("raindatatimestep");
                        activebox.Text = xreader.ReadElementString("activelayerthickness");

                        // more add on's  21/5/2012
                        downstreamshiftbox.Text = xreader.ReadElementString("downstreamshift");
                        courantbox.Text = xreader.ReadElementString("courantnumber");
                        textBox4.Text = xreader.ReadElementString("hflow");
                        textBox7.Text = xreader.ReadElementString("lateralsmoothing");
                        textBox8.Text = xreader.ReadElementString("froude_limit");
                        textBox9.Text = xreader.ReadElementString("mannings");

                        checkBox3.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TidalorStage"));
                        MinQmaxvalue.Text = xreader.ReadElementString("MinQmaxvalue");
                        TidalXmin.Text = xreader.ReadElementString("TidalXmin");
                        TidalXmax.Text = xreader.ReadElementString("TidalXmax");
                        TidalYmin.Text = xreader.ReadElementString("TidalYmin");
                        TidalYmax.Text = xreader.ReadElementString("TidalYmax");
                        TidalFileName.Text = xreader.ReadElementString("TidalFileName");
                        TidalInputStep.Text = xreader.ReadElementString("TidalInputStep");

                        // more add ons for bedrock erosion 19/1/14
                        bedrock_erosion_threshold_box.Text = xreader.ReadElementString("bedrock_erosion_threshold");
                        bedrock_erosion_rate_box.Text = xreader.ReadElementString("bedrock_erosion_rate");

                        // more add ons for spatially variable rainfall
                        rfnumBox.Text = xreader.ReadElementString("rfnum");
                        hydroindexBox.Text = xreader.ReadElementString("hydroindex");
                        checkBox7.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("MultiRainfall"));

                        // more addons for soil development and spatially variable mannings 28/8/2015
                        soildevbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("soildevbox"));
                        checkBox4.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("bedrocklowering"));
                        checkBox5.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("physicalweathering"));
                        checkBox6.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("chemicalweathering"));
                        textBox11.Text = xreader.ReadElementString("P1");
                        textBox12.Text = xreader.ReadElementString("b1");
                        textBox13.Text = xreader.ReadElementString("k1");
                        textBox14.Text = xreader.ReadElementString("c1");
                        textBox15.Text = xreader.ReadElementString("c2");
                        textBox16.Text = xreader.ReadElementString("k2");
                        textBox17.Text = xreader.ReadElementString("c3");
                        textBox18.Text = xreader.ReadElementString("c4");

                        SpatVarManningsCheckbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("SpatVarManningsCheckbox"));
                        textBox19.Text = xreader.ReadElementString("spatvarmanningsfilename");
                        mfiletimestepbox.Text = xreader.ReadElementString("mfiletimestepbox");
                        mvalueloadbox.Text = xreader.ReadElementString("mvalueloadbox");

                        // 5/12/16
                        meyerbox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("meyer"));

                        //18/7/18
                        checkBox8.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("all_nine_grainsizes"));

                        // 11/9/18
                        radioButton1.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("oldveg"));
                        radioButton2.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("newveg"));

                        // add ons from MDW
                        //spatialmanningsBox.Text = xreader.ReadElementString("manningfile");
                        //checkBox9.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("SpatialFriction"));
                        checkBox10.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TraceWater"));
                        checkBox11.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TraceRainZonation"));
                        textBox20.Text = xreader.ReadElementString("TraceRainZonationMapfile"); // MDW_V2 bug fix - corrected reference
                        checkBoxSoluteTracer.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TraceSolutes")); // MDW_V2

                        textBoxOutDir.Text = xreader.ReadElementString("OutputDirectory"); // MDW_V2
                        checkboxOutDirDateTime.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("OutDirDateTime")); // MDW_V2

                        // OIL_V1
                        OilTab_checkBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("OilSimulation"));
                        OilYmin.Text = xreader.ReadElementString("OilYmin");
                        OilYmax.Text = xreader.ReadElementString("OilYmax");
                        OilXmin.Text = xreader.ReadElementString("OilXmin");
                        OilXmax.Text = xreader.ReadElementString("OilXmax");
                        OilTimeMin.Text = xreader.ReadElementString("OilTimeMin");
                        OilDepthStart.Text = xreader.ReadElementString("OilDepthStart");
                        OilVolume.Text = xreader.ReadElementString("OilVolume");
                        OilSpillDuration.Text = xreader.ReadElementString("OilSpillDuration");

                        //MDW
                        xreader.ReadStartElement("SaveOptions");
                        dum = xreader.ReadElementString("Option");
                        menuItem6.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("SaveOptions");
                        dum = xreader.ReadElementString("Option");
                        menuItem15.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked"));
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("SaveOptions"); //MDW_V2
                        dum = xreader.ReadElementString("Option");
                        menuItemSoluteTracer.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked")); // MDW_V2
                        xreader.ReadEndElement();
                        //
                        xreader.ReadStartElement("SaveOptions"); //MDW reader bug fix, July 2026
                        dum = xreader.ReadElementString("Option");
                        menuItemOilSpill.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked")); // oil depth
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("SaveOptions"); //MDW reader bug fix, July 2026
                        dum = xreader.ReadElementString("Option");
                        menuItem17.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked")); // mass balance
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("SaveOptions"); //MDW reader bug fix, July 2026
                        dum = xreader.ReadElementString("Option");
                        menuItemOilconc.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("Checked")); // oil concentration
                        xreader.ReadEndElement();

                        // Jun LS tracing
                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        angle_thresholdbox.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                        xreader.ReadStartElement("Filenames");
                        dum = xreader.ReadElementString("Desc");
                        grain_index_file.Text = xreader.ReadElementString("Name");
                        xreader.ReadEndElement();
                        // more add ons for spatially variable tracer
                        tracer_num.Text = xreader.ReadElementString("tracer");
                        tracer_file.Text = xreader.ReadElementString("tracerindex");
                        checkBox_tracer.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("MultiTracer"));
                        // more add ons for spatially variable grainsize
                        landslide_grainsize.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("MultiGrain"));

                        // TEMP_V1 - water temperature module settings (own try/catch: safe against older config files without these elements)
                        try
                        {
                            TempTab_checkBox.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TempSimulate"));
                            isSimulateTemperature = TempTab_checkBox.Checked;

                            bool tempSimplified = XmlConvert.ToBoolean(xreader.ReadElementString("TempSimplifiedScheme"));
                            TempTab_radio_simplifiedscheme.Checked = tempSimplified;
                            TempTab_radio_fullscheme.Checked = !tempSimplified;
                            useSimplifiedTempScheme = tempSimplified;

                            TempTab_checkBox_hecraslongwave.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TempHecRasLongwave"));
                            useHecRasLongwave = TempTab_checkBox_hecraslongwave.Checked;

                            TempTab_checkBox_hecrasalbedo.Checked = XmlConvert.ToBoolean(xreader.ReadElementString("TempHecRasAlbedo"));
                            useHecRasAlbedo = TempTab_checkBox_hecrasalbedo.Checked;

                            TempTab_textBox_thermalinterval.Text = xreader.ReadElementString("TempThermalInterval");
                            double.TryParse(TempTab_textBox_thermalinterval.Text, out thermal_update_interval);

                            TempTab_textBox_mettimestep.Text = xreader.ReadElementString("TempMetTimeStep");
                            double.TryParse(TempTab_textBox_mettimestep.Text, out met_data_time_step);

                            TempTab_textBox_latitude.Text = xreader.ReadElementString("TempSiteLatitude");
                            double.TryParse(TempTab_textBox_latitude.Text, out siteLatitude);

                            TempTab_textBox_longitude.Text = xreader.ReadElementString("TempSiteLongitude");
                            double.TryParse(TempTab_textBox_longitude.Text, out siteLongitude);

                            TempTab_textBox_timezone.Text = xreader.ReadElementString("TempSiteTimeZone");
                            double.TryParse(TempTab_textBox_timezone.Text, out siteTimeZone);

                            TempTab_textBox_elevation.Text = xreader.ReadElementString("TempSiteElevation");
                            double.TryParse(TempTab_textBox_elevation.Text, out siteElevation);

                            TempTab_textBox_windheight.Text = xreader.ReadElementString("TempWindHeight");
                            double.TryParse(TempTab_textBox_windheight.Text, out windMeasurementHeight);

                            TempTab_textBox_initialtemp.Text = xreader.ReadElementString("TempInitialValue");
                            double.TryParse(TempTab_textBox_initialtemp.Text, out waterTempInitialValue);
                            TempTab_textBox_initialraster.Text = xreader.ReadElementString("TempInitialRaster");

                            TempTab_textBox_airtemp.Text = xreader.ReadElementString("TempFileAirTemp");
                            TempTab_textBox_shortwave.Text = xreader.ReadElementString("TempFileShortwave");
                            TempTab_textBox_windspeed.Text = xreader.ReadElementString("TempFileWindspeed");
                            TempTab_textBox_humidity.Text = xreader.ReadElementString("TempFileHumidity");
                            TempTab_textBox_cloudcover.Text = xreader.ReadElementString("TempFileCloudcover");
                            TempTab_textBox_pressure.Text = xreader.ReadElementString("TempFilePressure");
                            TempTab_textBox_dewpoint.Text = xreader.ReadElementString("TempFileDewpoint");
                            TempTab_textBox_sourcetemp.Text = xreader.ReadElementString("TempFileSourceTemp"); // TEMP_V1
                            TempTab_textBox_startdate.Text = xreader.ReadElementString("TempStartDateTime"); // TEMP_V1
                            DateTime.TryParse(TempTab_textBox_startdate.Text, out simulationStartDateTime);

                        }
                        catch (Exception eTemp)
                        {
                            MessageBox.Show("Error with loading the water quality configuration from the XML file." +
                                "\n\nDebug info: \n" + eTemp.Message + "\n\nStackTrace:\n" + eTemp.StackTrace);
                        }
                        ;

                        xreader.ReadEndElement();
                        xreader.ReadEndElement();
                    }
                    catch (Exception eXML)
                    {
                        MessageBox.Show("Error with loading XML file." +
                            "\n\nDebug info: \n" + eXML.Message + "\n\nStackTrace:\n" + eXML.StackTrace);
                    }
                    ;


                    xreader.Close();


                    this.Text = basetext + " (" + Path.GetFileName(cfgname) + ")";
                    button2.Enabled = true;
                    start_button.Enabled = false;
                    Panel1.Visible = false;
                    tabControl1.Visible = true;

                }
            }
        }
        // JMW - Config File Save & SaveAs Event Handler
        private void menuItemConfigFileSave_Click(object sender, System.EventArgs e)
        {
            XmlTextWriter xwriter;

            if ((sender == menuItemConfigFileSaveAs) || (cfgname == null))
            {

                SaveFileDialog saveFileDialog1 = new SaveFileDialog();

                saveFileDialog1.InitialDirectory = workdir;
                saveFileDialog1.Filter = "cfg files (*.xml)|*.xml|All files (*.*)|*.*";
                saveFileDialog1.FilterIndex = 1;
                saveFileDialog1.RestoreDirectory = false;

                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    cfgname = saveFileDialog1.FileName;
                }

            }

            if (cfgname != null)
            {

                //Create a new XmlTextWriter.
                xwriter = new XmlTextWriter(cfgname, System.Text.Encoding.UTF8);
                //Write the beginning of the document including the
                //document declaration. Standalone is true.
                //Use indentation for readability.
                xwriter.Formatting = Formatting.Indented;
                xwriter.Indentation = 4;


                xwriter.WriteStartDocument(true);

                //Write the beginning of the "data" element. This is
                //the opening tag to our data
                xwriter.WriteStartElement("Parms");
                xwriter.WriteStartElement("General-Parms");
                xwriter.WriteElementString("headeroverride", XmlConvert.ToString(overrideheaderBox.Checked));
                xwriter.WriteElementString("x-coordinate", xtextbox.Text);
                xwriter.WriteElementString("y-coordinate", ytextbox.Text);
                xwriter.WriteElementString("initscans", initscansbox.Text);
                xwriter.WriteElementString("maxerodelimit", erodefactorbox.Text);
                xwriter.WriteElementString("cellsize", dxbox.Text);
                xwriter.WriteElementString("memorylimit", limitbox.Text);
                xwriter.WriteElementString("minq", minqbox.Text);
                xwriter.WriteElementString("creeprate", creepratebox.Text);
                xwriter.WriteElementString("lateralerosionrate", lateralratebox.Text);
                xwriter.WriteElementString("maxiter", itermaxbox.Text);
                xwriter.WriteElementString("runstarttime", textBox1.Text);
                xwriter.WriteElementString("maxrunduration", cyclemaxbox.Text);
                xwriter.WriteElementString("slopefailurethreshold", slopebox.Text);
                xwriter.WriteElementString("wssmoothingradius", smoothbox.Text);
                xwriter.WriteElementString("mvalue", mvaluebox.Text);
                xwriter.WriteElementString("growgrasstime", grasstextbox.Text);
                xwriter.WriteElementString("initialq", textBox2.Text);
                xwriter.WriteElementString("wssmoothing", "false");
                xwriter.WriteElementString("grass-sediment", XmlConvert.ToString(flowonlybox.Checked));
                xwriter.WriteElementString("flowdistribution", textBox3.Text); // MJ 24/01/05
                xwriter.WriteElementString("mintimestep", mintimestepbox.Text); // MJ 24/01/05
                xwriter.WriteElementString("evaporation", k_evapBox.Text); // MJ 15/03/05
                xwriter.WriteElementString("vegcritshear", vegTauCritBox.Text); // MJ 10/05/05
                xwriter.WriteElementString("bedslope", XmlConvert.ToString(bedslope_box.Checked));
                xwriter.WriteElementString("wsslope", XmlConvert.ToString(false));
                xwriter.WriteElementString("veltaubox", XmlConvert.ToString(veltaubox.Checked));
                xwriter.WriteElementString("catchment_mode", XmlConvert.ToString(catchment_mode_box.Checked));
                xwriter.WriteElementString("reach_mode", XmlConvert.ToString(reach_mode_box.Checked));
                xwriter.WriteElementString("lat1", XmlConvert.ToString(false));
                xwriter.WriteElementString("lat2", XmlConvert.ToString(false));
                xwriter.WriteElementString("lat3", XmlConvert.ToString(false));
                xwriter.WriteElementString("cross_stream_grad", XmlConvert.ToString(0));
                xwriter.WriteElementString("max_vel", max_vel_box.Text);


                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "elevations");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem12.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "elevdiff");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem13.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "grainsize");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem14.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "total tracer g/s");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 1");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 2");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 3");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 4");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 5");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 6");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 7");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 8");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "tracer layer 9");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(false));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "water depth");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem25.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "d50");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem29.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "flow velocity");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem33.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "soil saturation");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem34.Checked));
                xwriter.WriteEndElement();

                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g1box.Text);
                xwriter.WriteElementString("gp", gp1box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS1box.Checked));
                xwriter.WriteElementString("fv", fallGS1box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g2box.Text);
                xwriter.WriteElementString("gp", gp2box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS2box.Checked));
                xwriter.WriteElementString("fv", fallGS2box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g3box.Text);
                xwriter.WriteElementString("gp", gp3box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS3box.Checked));
                xwriter.WriteElementString("fv", fallGS3box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g4box.Text);
                xwriter.WriteElementString("gp", gp4box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS4box.Checked));
                xwriter.WriteElementString("fv", fallGS4box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g5box.Text);
                xwriter.WriteElementString("gp", gp5box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS5box.Checked));
                xwriter.WriteElementString("fv", fallGS5box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g6box.Text);
                xwriter.WriteElementString("gp", gp6box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS6box.Checked));
                xwriter.WriteElementString("fv", fallGS6box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g7box.Text);
                xwriter.WriteElementString("gp", gp7box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS7box.Checked));
                xwriter.WriteElementString("fv", fallGS7box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g8box.Text);
                xwriter.WriteElementString("gp", gp8box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS8box.Checked));
                xwriter.WriteElementString("fv", fallGS8box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g9box.Text);
                xwriter.WriteElementString("gp", gp9box.Text);
                xwriter.WriteElementString("ss", XmlConvert.ToString(suspGS9box.Checked));
                xwriter.WriteElementString("fv", fallGS9box.Text);
                xwriter.WriteEndElement();

                // additional landslide grainsize
                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g1_box.Text);
                xwriter.WriteElementString("gp", gp1_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g2_box.Text);
                xwriter.WriteElementString("gp", gp2_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g3_box.Text);
                xwriter.WriteElementString("gp", gp3_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g4_box.Text);
                xwriter.WriteElementString("gp", gp4_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g5_box.Text);
                xwriter.WriteElementString("gp", gp5_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g6_box.Text);
                xwriter.WriteElementString("gp", gp6_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g7_box.Text);
                xwriter.WriteElementString("gp", gp7_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g8_box.Text);
                xwriter.WriteElementString("gp", gp8_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Grain-Size");
                xwriter.WriteElementString("gs", g9_box.Text);
                xwriter.WriteElementString("gp", gp9_box.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("File-Parms");

                xwriter.WriteElementString("inputtimestep", input_time_step_box.Text);
                xwriter.WriteElementString("saveinterval", saveintervalbox.Text);
                xwriter.WriteElementString("savetologfileinterval", outputfilesaveintervalbox.Text);
                xwriter.WriteElementString("tracerrun", XmlConvert.ToString(tracerbox.Checked));
                xwriter.WriteElementString("uniquefilecheck", XmlConvert.ToString(uniquefilecheck.Checked));


                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "DEM Data File");
                xwriter.WriteElementString("Name", openfiletextbox.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Grain Data File");
                xwriter.WriteElementString("Name", graindataloadbox.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Bedrock Data File");
                xwriter.WriteElementString("Name", bedrockbox.Text);
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Rain Data File");
                xwriter.WriteElementString("Name", raindataloadbox.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Tracer File");
                xwriter.WriteElementString("Name", mine_input_textBox.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Tracer Sed Vol File");
                xwriter.WriteElementString("Name", tracerhydrofile.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Tracer Grain Size Data File");
                xwriter.WriteElementString("Name", "null");
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox1.Checked));
                xwriter.WriteElementString("X", xbox1.Text);
                xwriter.WriteElementString("Y", ybox1.Text);
                xwriter.WriteElementString("Filename", infile1.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox2.Checked));
                xwriter.WriteElementString("X", xbox2.Text);
                xwriter.WriteElementString("Y", ybox2.Text);
                xwriter.WriteElementString("Filename", infile2.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox3.Checked));
                xwriter.WriteElementString("X", xbox3.Text);
                xwriter.WriteElementString("Y", ybox3.Text);
                xwriter.WriteElementString("Filename", infile3.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox4.Checked));
                xwriter.WriteElementString("X", xbox4.Text);
                xwriter.WriteElementString("Y", ybox4.Text);
                xwriter.WriteElementString("Filename", infile4.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox5.Checked));
                xwriter.WriteElementString("X", xbox5.Text);
                xwriter.WriteElementString("Y", ybox5.Text);
                xwriter.WriteElementString("Filename", infile5.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox6.Checked));
                xwriter.WriteElementString("X", xbox6.Text);
                xwriter.WriteElementString("Y", ybox6.Text);
                xwriter.WriteElementString("Filename", infile6.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox7.Checked));
                xwriter.WriteElementString("X", xbox7.Text);
                xwriter.WriteElementString("Y", ybox7.Text);
                xwriter.WriteElementString("Filename", infile7.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Sources");
                xwriter.WriteElementString("input", XmlConvert.ToString(inbox8.Checked));
                xwriter.WriteElementString("X", xbox8.Text);
                xwriter.WriteElementString("Y", ybox8.Text);
                xwriter.WriteElementString("Filename", infile8.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("ExtraSources");                                      //MDW_V2
                xwriter.WriteElementString("input", XmlConvert.ToString(inboxExtra.Checked));   //
                xwriter.WriteElementString("Filename", infileExtra.Text);                       //
                xwriter.WriteEndElement();                                                      //
                xwriter.WriteEndElement();


                xwriter.WriteStartElement("Description");
                xwriter.WriteElementString("S", DescBox.Text);
                xwriter.WriteEndElement();

                //JMW 2004-11-11; updated MJ 24/01/05
                xwriter.WriteStartElement("OutputFile-Parms");
                xwriter.WriteElementString("generateavifile", "false");
                xwriter.WriteElementString("avifile", "novalue");
                xwriter.WriteElementString("avifreq", saveintervalbox.Text);
                xwriter.WriteElementString("generatetimeseriesfile", XmlConvert.ToString(checkBoxGenerateTimeSeries.Checked));
                xwriter.WriteElementString("timeseriesfile", TimeseriesOutBox.Text);
                xwriter.WriteElementString("timeseriesfreq", outputfilesaveintervalbox.Text);
                xwriter.WriteElementString("generateiterationsfile", XmlConvert.ToString(checkBoxGenerateIterations.Checked));
                xwriter.WriteElementString("iterationsfile", IterationOutbox.Text);
                xwriter.WriteEndElement();


                xwriter.WriteStartElement("Display");
                xwriter.WriteElementString("top", string.Format(" {0}", Form1.ActiveForm.Top));
                xwriter.WriteElementString("left", string.Format(" {0}", Form1.ActiveForm.Left));
                xwriter.WriteElementString("width", string.Format(" {0}", Form1.ActiveForm.Width));
                xwriter.WriteElementString("height", string.Format(" {0}", Form1.ActiveForm.Height));
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Lateral");
                xwriter.WriteElementString("oldlat", XmlConvert.ToString(false));
                xwriter.WriteElementString("newlat", XmlConvert.ToString(newlateral.Checked));
                xwriter.WriteEndElement();

                xwriter.WriteStartElement("Add_Ons");
                xwriter.WriteElementString("tracer-out", XmlConvert.ToString(tracerOutcheckBox.Checked));
                xwriter.WriteElementString("tracer-out-filename", tracerOutputtextBox.Text);
                xwriter.WriteElementString("google_animation", XmlConvert.ToString(googleAnimationCheckbox.Checked));
                xwriter.WriteElementString("google_animation_file_name", googleAnimationTextBox.Text);
                xwriter.WriteElementString("google_begin", googleBeginDate.Text);
                xwriter.WriteElementString("google_interval", googAnimationSaveInterval.Text);
                xwriter.WriteElementString("jMean", XmlConvert.ToString(jmeaninputfilebox.Checked));
                xwriter.WriteElementString("edge_smoothing", avge_smoothbox.Text);
                xwriter.WriteElementString("displacement", XmlConvert.ToString(false));
                xwriter.WriteElementString("prop_remain", propremaining.Text);
                xwriter.WriteElementString("max_time_step", max_time_step_Box.Text);
                xwriter.WriteElementString("contam_input", XmlConvert.ToString(false));
                xwriter.WriteElementString("mineX", "null");
                xwriter.WriteElementString("mineY", "null");
                xwriter.WriteElementString("contam_input_file", "null");
                xwriter.WriteElementString("soil_rate", soil_ratebox.Text);
                xwriter.WriteElementString("siberia", XmlConvert.ToString(SiberiaBox.Checked));
                xwriter.WriteElementString("beta1", Beta1Box.Text);
                xwriter.WriteElementString("beta3", Beta3Box.Text);
                xwriter.WriteElementString("m1", m1Box.Text);
                xwriter.WriteElementString("m3", m3Box.Text);
                xwriter.WriteElementString("n1", n1Box.Text);
                xwriter.WriteElementString("W_depth_erosion_threshold", Q2box.Text);
                xwriter.WriteElementString("fexp", XmlConvert.ToString(1));
                xwriter.WriteElementString("div_inputs", div_inputs_box.Text);
                xwriter.WriteElementString("initial_sand_depth", init_depth_box.Text);
                xwriter.WriteElementString("maxslabdepth", slab_depth_box.Text);
                xwriter.WriteElementString("angle", shadow_angle_box.Text);
                xwriter.WriteElementString("checkup", upstream_check_box.Text);
                xwriter.WriteElementString("dep_probability", depo_prob_box.Text);
                xwriter.WriteElementString("downstream_offset", offset_box.Text);
                xwriter.WriteElementString("dune_timestep", dune_time_box.Text);
                xwriter.WriteElementString("dune_gridsize", dune_grid_size_box.Text);
                xwriter.WriteElementString("wilcock", XmlConvert.ToString(wilcockbox.Checked));
                xwriter.WriteElementString("einstein", XmlConvert.ToString(einsteinbox.Checked));
                xwriter.WriteElementString("dune", XmlConvert.ToString(DuneBox.Checked));
                // three UTM interface elements
                xwriter.WriteElementString("UTM", XmlConvert.ToString(UTMgridcheckbox.Checked));
                xwriter.WriteElementString("South", XmlConvert.ToString(UTMsouthcheck.Checked));
                xwriter.WriteElementString("UTMzone", UTMzonebox.Text);

                xwriter.WriteElementString("raindatatimestep", raintimestepbox.Text);
                xwriter.WriteElementString("activelayerthickness", activebox.Text);

                // more add on's  21/5/2012

                xwriter.WriteElementString("downstreamshift", downstreamshiftbox.Text);
                xwriter.WriteElementString("courantnumber", courantbox.Text);
                xwriter.WriteElementString("hflow", textBox4.Text);
                xwriter.WriteElementString("lateralsmoothing", textBox7.Text);
                xwriter.WriteElementString("froude_limit", textBox8.Text);
                xwriter.WriteElementString("mannings", textBox9.Text);

                // more add on's 4/7/13
                xwriter.WriteElementString("TidalorStage", XmlConvert.ToString(checkBox3.Checked));
                xwriter.WriteElementString("MinQmaxvalue", MinQmaxvalue.Text);
                xwriter.WriteElementString("TidalXmin", TidalXmin.Text);
                xwriter.WriteElementString("TidalXmax", TidalXmax.Text);
                xwriter.WriteElementString("TidalYmin", TidalYmin.Text);
                xwriter.WriteElementString("TidalYmax", TidalYmax.Text);
                xwriter.WriteElementString("TidalFileName", TidalFileName.Text);
                xwriter.WriteElementString("TidalInputStep", TidalInputStep.Text);

                // more add ons for bedrock erosion 19/1/14
                xwriter.WriteElementString("bedrock_erosion_threshold", bedrock_erosion_threshold_box.Text);
                xwriter.WriteElementString("bedrock_erosion_rate", bedrock_erosion_rate_box.Text);

                // more add ons for spatially variable rainfall
                xwriter.WriteElementString("rfnum", rfnumBox.Text);
                xwriter.WriteElementString("hydroindex", hydroindexBox.Text);
                xwriter.WriteElementString("MultiRainfall", XmlConvert.ToString(checkBox7.Checked));

                // more addons for soil development and spatially variable mannings 28/8/2015
                xwriter.WriteElementString("soildevbox", XmlConvert.ToString(soildevbox.Checked));
                xwriter.WriteElementString("bedrocklowering", XmlConvert.ToString(checkBox4.Checked));
                xwriter.WriteElementString("physicalweathering", XmlConvert.ToString(checkBox5.Checked));
                xwriter.WriteElementString("chemicalweathering", XmlConvert.ToString(checkBox6.Checked));
                xwriter.WriteElementString("P1", textBox11.Text);
                xwriter.WriteElementString("b1", textBox12.Text);
                xwriter.WriteElementString("k1", textBox13.Text);
                xwriter.WriteElementString("c1", textBox14.Text);
                xwriter.WriteElementString("c2", textBox15.Text);
                xwriter.WriteElementString("k2", textBox16.Text);
                xwriter.WriteElementString("c3", textBox17.Text);
                xwriter.WriteElementString("c4", textBox18.Text);

                xwriter.WriteElementString("SpatVarManningsCheckbox", XmlConvert.ToString(SpatVarManningsCheckbox.Checked));
                xwriter.WriteElementString("spatvarmanningsfilename", textBox19.Text);

                // 4/10/15 spat variable mannings box
                xwriter.WriteElementString("mfiletimestepbox", mfiletimestepbox.Text);
                xwriter.WriteElementString("mvalueloadbox", mvalueloadbox.Text);

                // 5/12/16
                xwriter.WriteElementString("meyer", XmlConvert.ToString(meyerbox.Checked));
                //18/7/18
                xwriter.WriteElementString("all_nine_grainsizes", XmlConvert.ToString(checkBox8.Checked));
                // 11/9/18
                xwriter.WriteElementString("oldveg", XmlConvert.ToString(radioButton1.Checked));
                xwriter.WriteElementString("newveg", XmlConvert.ToString(radioButton2.Checked));

                // add ons from MDW
                //xwriter.WriteElementString("manningfile", spatialmanningsBox.Text);
                //xwriter.WriteElementString("SpatialFriction", XmlConvert.ToString(checkBox9.Checked));
                xwriter.WriteElementString("TraceWater", XmlConvert.ToString(checkBox10.Checked));
                xwriter.WriteElementString("TraceRainZonation", XmlConvert.ToString(checkBox11.Checked));
                xwriter.WriteElementString("TraceRainZonationMapfile", textBox20.Text); // MDW_V2 bug fix - corrected reference
                xwriter.WriteElementString("TraceSolutes", XmlConvert.ToString(checkBoxSoluteTracer.Checked));

                xwriter.WriteElementString("OutputDirectory", textBoxOutDir.Text); // MDW_V2
                xwriter.WriteElementString("OutDirDateTime", XmlConvert.ToString(checkboxOutDirDateTime.Checked)); // MDW_V2

                // OIL_V1
                xwriter.WriteElementString("OilSimulation", XmlConvert.ToString(OilTab_checkBox.Checked));
                xwriter.WriteElementString("OilYmin", OilYmin.Text);
                xwriter.WriteElementString("OilYmax", OilYmax.Text);
                xwriter.WriteElementString("OilXmin", OilXmin.Text);
                xwriter.WriteElementString("OilXmax", OilXmax.Text);
                xwriter.WriteElementString("OilTimeMin", OilTimeMin.Text);
                xwriter.WriteElementString("OilDepthStart", OilDepthStart.Text);
                xwriter.WriteElementString("OilVolume", OilVolume.Text);
                xwriter.WriteElementString("OilSpillDuration", OilSpillDuration.Text);

                //MDW
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "water tracers");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem6.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "rain zone tracers");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem15.Checked));
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "solute tracers");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItemSoluteTracer.Checked));
                xwriter.WriteEndElement();
                //
                //xwriter.WriteElementString();

                //PDF
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "oil depth");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItemOilSpill.Checked));
                xwriter.WriteEndElement();
                //

                //PDF
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "mass balance");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItem17.Checked));
                xwriter.WriteEndElement();
                //

                //PDF
                xwriter.WriteStartElement("SaveOptions");
                xwriter.WriteElementString("Option", "oil concentration");
                xwriter.WriteElementString("Checked", XmlConvert.ToString(menuItemOilconc.Checked));
                xwriter.WriteEndElement();
                //

                //additional from Jun LS tracing
                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Angle Data File");
                xwriter.WriteElementString("Name", angle_thresholdbox.Text);
                xwriter.WriteEndElement();
                xwriter.WriteStartElement("Filenames");
                xwriter.WriteElementString("Desc", "Grain index Data File");
                xwriter.WriteElementString("Name", grain_index_file.Text);
                xwriter.WriteEndElement();
                xwriter.WriteElementString("tracer", tracer_num.Text);
                xwriter.WriteElementString("tracerindex", tracer_file.Text);
                xwriter.WriteElementString("MultiTracer", XmlConvert.ToString(checkBox_tracer.Checked));
                // more add ons for spatially variable grainsize
                xwriter.WriteElementString("MultiGrain", XmlConvert.ToString(landslide_grainsize.Checked));

                // TEMP_V1 - water temperature module settings
                xwriter.WriteElementString("TempSimulate", XmlConvert.ToString(TempTab_checkBox.Checked));
                xwriter.WriteElementString("TempSimplifiedScheme", XmlConvert.ToString(TempTab_radio_simplifiedscheme.Checked));
                xwriter.WriteElementString("TempHecRasLongwave", XmlConvert.ToString(TempTab_checkBox_hecraslongwave.Checked));
                xwriter.WriteElementString("TempHecRasAlbedo", XmlConvert.ToString(TempTab_checkBox_hecrasalbedo.Checked));
                xwriter.WriteElementString("TempThermalInterval", TempTab_textBox_thermalinterval.Text);
                xwriter.WriteElementString("TempMetTimeStep", TempTab_textBox_mettimestep.Text);
                xwriter.WriteElementString("TempSiteLatitude", TempTab_textBox_latitude.Text);
                xwriter.WriteElementString("TempSiteLongitude", TempTab_textBox_longitude.Text);
                xwriter.WriteElementString("TempSiteTimeZone", TempTab_textBox_timezone.Text);
                xwriter.WriteElementString("TempSiteElevation", TempTab_textBox_elevation.Text);
                xwriter.WriteElementString("TempWindHeight", TempTab_textBox_windheight.Text);
                xwriter.WriteElementString("TempInitialValue", TempTab_textBox_initialtemp.Text);
                xwriter.WriteElementString("TempInitialRaster", TempTab_textBox_initialraster.Text);
                xwriter.WriteElementString("TempFileAirTemp", TempTab_textBox_airtemp.Text);
                xwriter.WriteElementString("TempFileShortwave", TempTab_textBox_shortwave.Text);
                xwriter.WriteElementString("TempFileWindspeed", TempTab_textBox_windspeed.Text);
                xwriter.WriteElementString("TempFileHumidity", TempTab_textBox_humidity.Text);
                xwriter.WriteElementString("TempFileCloudcover", TempTab_textBox_cloudcover.Text);
                xwriter.WriteElementString("TempFilePressure", TempTab_textBox_pressure.Text);
                xwriter.WriteElementString("TempFileDewpoint", TempTab_textBox_dewpoint.Text);
                xwriter.WriteElementString("TempFileSourceTemp", TempTab_textBox_sourcetemp.Text); // TEMP_V1
                xwriter.WriteElementString("TempStartDateTime", TempTab_textBox_startdate.Text); // TEMP_V1


                xwriter.WriteEndElement();
                xwriter.WriteEndElement();


                //End the document
                xwriter.WriteEndDocument();

                //Flush the xml document to the underlying stream and
                //close the underlying stream. The data will not be
                //written out to the stream until either the Flush()
                //method is called or the Close() method is called.
                xwriter.Close();

                this.Text = basetext + " (" + Path.GetFileName(cfgname) + ")";
            }
        }
        private void suspCheckedChange(object sender, System.EventArgs e)
        {
            fallGS1box.Enabled = suspGS1box.Checked;
            fallGS2box.Enabled = suspGS2box.Checked;
            fallGS3box.Enabled = suspGS3box.Checked;
            fallGS4box.Enabled = suspGS4box.Checked;
            fallGS5box.Enabled = suspGS5box.Checked;
            fallGS6box.Enabled = suspGS6box.Checked;
            fallGS7box.Enabled = suspGS7box.Checked;
            fallGS8box.Enabled = suspGS8box.Checked;
            fallGS9box.Enabled = suspGS9box.Checked;
        }
        private void fracGSchanged(object sender, System.EventArgs e)
        {
            double sum;

            sum = 0.0;
            if (gp1box.Text != "") sum += double.Parse(gp1box.Text);
            if (gp2box.Text != "") sum += double.Parse(gp2box.Text);
            if (gp3box.Text != "") sum += double.Parse(gp3box.Text);
            if (gp4box.Text != "") sum += double.Parse(gp4box.Text);
            if (gp5box.Text != "") sum += double.Parse(gp5box.Text);
            if (gp6box.Text != "") sum += double.Parse(gp6box.Text);
            if (gp7box.Text != "") sum += double.Parse(gp7box.Text);
            if (gp8box.Text != "") sum += double.Parse(gp8box.Text);
            if (gp9box.Text != "") sum += double.Parse(gp9box.Text);

            if (Math.Abs(sum - 1.0) < 0.000000001)
            {
                gpSumLabel.Text = "OK";
                gpSumLabel.ForeColor = Color.Black;
                gpSumLabel2.ForeColor = Color.Black;
            }
            else
            {
                gpSumLabel.Text = string.Format("{0:F8}", sum);
                gpSumLabel.ForeColor = Color.Red;
                gpSumLabel2.ForeColor = Color.Red;
            }
        }
        private void button4_Click_1(object sender, System.EventArgs e)
        {
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
        private void overrideheaderBox_CheckedChanged(object sender, System.EventArgs e)
        {
            label1.Enabled = overrideheaderBox.Checked;
            label2.Enabled = overrideheaderBox.Checked;
            label11.Enabled = overrideheaderBox.Checked;
            xtextbox.Enabled = overrideheaderBox.Checked;
            ytextbox.Enabled = overrideheaderBox.Checked;
            dxbox.Enabled = overrideheaderBox.Checked;
        }
        private void checkBox1_CheckedChanged(object sender, System.EventArgs e)
        {

        }
        private void bedslope_box_CheckedChanged(object sender, System.EventArgs e)
        {
            if (bedslope_box.Checked == true)
            {
                veltaubox.Checked = false;
                bedslopebox2.Checked = false;
            }
        }
        private void newlateral_CheckedChanged(object sender, System.EventArgs e)
        {

            if (newlateral.Checked == true) nolateral.Checked = false;
        }
        private void label54_Click(object sender, System.EventArgs e)
        {

        }
        private void button5_Click(object sender, System.EventArgs e)
        {
            Form1.ActiveForm.Show();
        }
        private void button5_Click_1(object sender, System.EventArgs e)
        {
            get_area();
        }
        private void veltaubox_CheckedChanged(object sender, EventArgs e)
        {
            if (veltaubox.Checked == true)
            {

                bedslope_box.Checked = false;
                bedslopebox2.Checked = false;
            }
        }
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            erodedepo();
        }
        private void nolateral_CheckedChanged(object sender, EventArgs e)
        {

            if (nolateral.Checked == true) newlateral.Checked = false;
        }
        private void Form1_Resize(object sender, EventArgs e)
        {
            zoomPanImageBox1.Height = this.Height - 225;
            zoomPanImageBox1.Width = this.Width - 20;
        }
        private void zoomPanImageBox1_Load(object sender, EventArgs e)
        {

        }
        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            contrastMultiplier = contrastFactor[trackBar1.Value];
            drawwater(mygraphics);
        }
        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            magnifyValue = zoomFactor[this.trackBar2.Value];
            zoomPanImageBox1.setZoom();
        }
        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            enhanceValue = enhanceFactor[this.trackBar3.Value];
            drawwater(mygraphics);
        }
        private void comboBox1_SelectedValueChanged(object sender, EventArgs e)
        {
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }

        // Water tracer controls - MDW
        private void comboBox2_SelectedValueChanged(object sender, EventArgs e)
        {
            updateClick = 1;
            if (checkBox12.Checked == true) tracerain_rgb[0] = comboBox2.SelectedIndex;
            else if (checkBoxSoluteVis.Checked == true) tracesolute_rgb[0] = comboBox2.SelectedIndex;
            else trace_rgb[0] = comboBox2.SelectedIndex;
            if (tracergb_initialising == false)
            {
                this.Refresh();
                drawwater(mygraphics);
            }
        }
        private void comboBox3_SelectedValueChanged(object sender, EventArgs e)
        {
            updateClick = 1;
            if (checkBox12.Checked == true) tracerain_rgb[1] = comboBox3.SelectedIndex;
            else if (checkBoxSoluteVis.Checked == true) tracesolute_rgb[1] = comboBox3.SelectedIndex;
            else trace_rgb[1] = comboBox3.SelectedIndex;
            if (tracergb_initialising == false)
            {
                this.Refresh();
                drawwater(mygraphics);
            }
        }
        private void comboBox4_SelectedValueChanged(object sender, EventArgs e)
        {
            updateClick = 1;
            if (checkBox12.Checked == true) tracerain_rgb[2] = comboBox4.SelectedIndex;
            else if (checkBoxSoluteVis.Checked == true) tracesolute_rgb[2] = comboBox4.SelectedIndex;
            else trace_rgb[2] = comboBox4.SelectedIndex;
            if (tracergb_initialising == false)
            {
                this.Refresh();
                drawwater(mygraphics);
            }
        }

        private void n1Box_TextChanged(object sender, EventArgs e)
        {

        }
        private void graphicToGoogleEarthButton_Click(object sender, EventArgs e)
        {
            if (coordinateDone == 0)
            {
                //transfrom coordinates
                point testPoint = new point(xll, yll);
                if (UTMgridcheckbox.Checked)
                {
                    testPoint.UTMzone = System.Convert.ToInt32(UTMzonebox.Text);
                    testPoint.south = System.Convert.ToBoolean(UTMsouthcheck.Checked);
                    testPoint.transformUTMPoint();
                }
                else
                {
                    testPoint.transformPoint();
                }
                yurcorner = yll + (System.Convert.ToDouble(ymax) * System.Convert.ToDouble(DX));
                xurcorner = xll + (System.Convert.ToDouble(xmax) * System.Convert.ToDouble(DX));
                point testPoint2 = new point(xurcorner, yurcorner);
                if (UTMgridcheckbox.Checked)
                {
                    testPoint2.UTMzone = System.Convert.ToInt32(UTMzonebox.Text);
                    testPoint2.south = System.Convert.ToBoolean(UTMsouthcheck.Checked);
                    testPoint2.transformUTMPoint();
                }
                else
                {
                    testPoint2.transformPoint();
                }



                urfinalLati = testPoint2.ycoord;
                urfinalLongi = testPoint2.xcoord;
                llfinalLati = testPoint.ycoord;
                llfinalLongi = testPoint.xcoord;
                coordinateDone = 1;
            }

            //Save image
            m_objDrawingSurface.MakeTransparent();
            m_objDrawingSurface.Save(Path.Combine(googleAnimationDir, @"mysavedimage" + imageCount + ".png"), // MDW_V2
                                     System.Drawing.Imaging.ImageFormat.Png);
            //m_objDrawingSurface.Save(@"mysavedimage" + imageCount + ".png", System.Drawing.Imaging.ImageFormat.Png);
            //create kml file for image
            string kml_file_name = "image" + imageCount + ".kml";
            StreamWriter kmlsr = File.CreateText(kml_file_name);
            string kml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
                                 <kml xmlns=""http://earth.google.com/kml/2.1"">
                                 <GroundOverlay>
                                 	<name>Untitled Image Overlay</name>";
            kml = kml + "\n<Icon>"
                   + "\n<href>mySavedImage" + imageCount + ".png</href>"
                   + "\n</Icon>"
                   + "\n<LatLonBox>";
            kml = kml + "\n<north>" + urfinalLati + "</north>"
                      + "\n<south>" + llfinalLati + "</south>"
                      + "\n<east>" + urfinalLongi + "</east>"
                      + "\n<west>" + llfinalLongi + "</west>\n";
            kml = kml + @"</LatLonBox>
                                 </GroundOverlay>
                                 </kml>
                                             ";
            kmlsr.Write(kml);
            kmlsr.Close();
            imageCount++;
        }
        private void einsteinbox_CheckedChanged(object sender, EventArgs e)
        {
            if (wilcockbox.Checked == true) wilcockbox.Checked = false;
            if (meyerbox.Checked == true) meyerbox.Checked = false;
        }
        private void wilcockbox_CheckedChanged(object sender, EventArgs e)
        {
            if (einsteinbox.Checked == true) einsteinbox.Checked = false;
            if (meyerbox.Checked == true) meyerbox.Checked = false;
        }
        private void HydrologyTab_Click(object sender, EventArgs e)
        {

        }
        private void OilTab_Click(object sender, EventArgs e) // OIL_V1
        {

        }

        private void button6_Click(object sender, EventArgs e)
        {
            int x, y;
            for (x = 1; x <= xmax; x++)
            {
                for (y = 1; y <= ymax; y++)
                {
                    if (x > 0 && x <= 200) elev[x, y] += 1;
                }
            }
        }
        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false) tabControl1.Visible = false;
            if (checkBox1.Checked == true) tabControl1.Visible = true;
        }
        private void tabPage5_Click(object sender, EventArgs e)
        {

        }
        private void label88_Click(object sender, EventArgs e)
        {

        }
        private void textBox12_TextChanged(object sender, EventArgs e)
        {

        }
        private void label58_Click(object sender, EventArgs e)
        {

        }
        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (UTMgridcheckbox.Checked)
            {
                UTMzonebox.Visible = true;
                textBox6.Visible = true;
                UTMsouthcheck.Visible = true;
                groupBox4.Visible = true;
            }
        }
        private void bedslopebox2_CheckedChanged(object sender, EventArgs e)
        {
            if (bedslopebox2.Checked == true)
            {
                veltaubox.Checked = false;
                bedslope_box.Checked = false;


            }
        }
        private void UTMgridcheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (UTMgridcheckbox.Checked)
            {
                UTMzonebox.Visible = true;
                textBox6.Visible = true;
                UTMsouthcheck.Visible = true;
                groupBox4.Visible = true;
            }

        }
        private void mouseclick2(object sender, MouseEventArgs e)
        {

        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {

            if (checkBox2.Checked == true)
            {
                CAESAR_lisflood_1._0.Form2 secondForm = new CAESAR_lisflood_1._0.Form2();
                secondForm.Show();

            }

        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox7.Checked == true)
            {
                rfnumBox.Enabled = true; // MDW_V2 changed each to Enable rather than visible
                hydroindexBox.Enabled = true;
                label102.Enabled = true;
                label103.Enabled = true;
            }
            else // MDW_V2 added to disable when unchecked
            {
                rfnumBox.Enabled = false;
                hydroindexBox.Enabled = false;
                label102.Enabled = false;
                label103.Enabled = false;
            }
        }

        private void SpatVarManningsCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            if (SpatVarManningsCheckbox.Checked == true)
            {
                textBox19.Visible = true;
                label104.Visible = true;

            }
        }

        private void meyerbox_CheckedChanged(object sender, EventArgs e)
        {
            if (wilcockbox.Checked == true) wilcockbox.Checked = false;
            if (einsteinbox.Checked == true) einsteinbox.Checked = false;
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox10.Checked == true)
            {
                isTraceWater = true;
                checkBox11.Enabled = true; //MDW_V2 changed from visible, so it shows greyed out by default
                textBox20.Enabled = true;  //
                checkBoxSoluteTracer.Enabled = true; //MDW_V2
                                                     //labelSoluteNumber.Enabled = true;
            }
            else
            {
                isTraceWater = false;
                checkBox11.Enabled = false; //MDW_V2 changed from visible, so it shows greyed out by default
                textBox20.Enabled = false;  //
                checkBoxSoluteTracer.Enabled = false; //MDW_V2
                                                      //labelSoluteNumber.Enabled = false;
            }
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox11.Checked == true)
            {
                isTraceRainZonation = true;
                checkBox12.Enabled = true; // graphics
            }
            else
            {
                isTraceRainZonation = false;
                checkBox12.Enabled = false; // graphics
            }
        }

        private void checkBoxSoluteTracer_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxSoluteTracer.Checked == true)
            {
                isTraceSolutes = true;
                //checkBoxSoluteView.Enabled = true;
            }
            else
            {
                isTraceSolutes = false;
                //checkBoxSoluteView.Enabled = false;
            }
        }

        private void checkBox12_CheckedChanged(object sender, EventArgs e)
        {
            tracergb_initialising = true;

            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            comboBox4.Items.Clear();
            //comboBox2.Items.Add(" ");
            //comboBox3.Items.Add(" ");
            //comboBox4.Items.Add(" ");
            comboBox2.Items.Add("0 : [none]"); // MDW_V2
            comboBox3.Items.Add("0 : [none]"); // MDW_V2
            comboBox4.Items.Add("0 : [none]"); // MDW_V2

            if (checkBox12.Checked == true)
            {

                if (checkBoxSoluteVis.Checked == true) // MDW_V2 - both can't be active simultaneously
                {
                    checkBoxSoluteVis.Checked = false;
                }

                // Add rain zones to the lists
                for (int z = 0; z < nRainZones; z++) // MDW_V2 updated index and string below
                {
                    comboBox2.Items.Add(Convert.ToString(z + 1) + " : rain zone " + Convert.ToString(rainZones[z]));
                    comboBox3.Items.Add(Convert.ToString(z + 1) + " : rain zone " + Convert.ToString(rainZones[z]));
                    comboBox4.Items.Add(Convert.ToString(z + 1) + " : rain zone " + Convert.ToString(rainZones[z]));
                }
                // Assign selected layer for first time
                if (tracerain_rgb[0] == -1)
                {
                    comboBox2.SelectedIndex = 1; tracerain_rgb[0] = 1;
                    if (nRainZones > 1) { comboBox3.SelectedIndex = 2; tracerain_rgb[1] = 2; } // does not default to zero if not loaded
                    else { comboBox3.SelectedIndex = 0; tracerain_rgb[1] = 0; }
                    if (nRainZones > 2) { comboBox4.SelectedIndex = 3; tracerain_rgb[2] = 3; } // does not default to zero if not loaded
                    else { comboBox4.SelectedIndex = 0; tracerain_rgb[2] = 0; }

                }
                // or to the one previously selected
                else
                {
                    comboBox2.SelectedIndex = tracerain_rgb[0];
                    comboBox3.SelectedIndex = tracerain_rgb[1];
                    comboBox4.SelectedIndex = tracerain_rgb[2];
                }
            }
            else
            {

                if (checkBoxSoluteVis.Checked == true) // MDW_V2 - both can't be active simultaneously
                {
                    return; // do nothing: checkBoxSoluteVis_CheckedChanged will handle this
                }

                for (int z = 0; z < nSources; z++) // MDW_V2 updated to zero index
                {
                    // MDW_V2 - source selector box string generation, updated indices
                    string comboText = "";
                    if (z == 0) // tide source
                    {
                        if (checkBox3.Checked == true)
                        {
                            comboText = Convert.ToString(z + 1) + " : Stage";
                        }
                        else
                        {
                            comboText = Convert.ToString(z + 1) + " : Stage [not active]";
                        }
                    }
                    else if (z == 1) // rain source
                    {
                        if (catchment_mode_box.Checked == true)
                        {
                            comboText = Convert.ToString(z + 1) + " : Rain";
                        }
                        else
                        {
                            comboText = Convert.ToString(z + 1) + " : Rain [not active]";
                        }
                    }
                    else if (z >= 2) // hydro sources
                    {
                        comboText = Convert.ToString(z + 1) + " : " + inputfilenames[z - sourceIndexAddition];
                    }
                    comboBox2.Items.Add(comboText);
                    comboBox3.Items.Add(comboText);
                    comboBox4.Items.Add(comboText);
                }
                // Assign selected layer to the one previously selected
                comboBox2.SelectedIndex = trace_rgb[0];
                comboBox3.SelectedIndex = trace_rgb[1];
                comboBox4.SelectedIndex = trace_rgb[2];
            }
            tracergb_initialising = false;
            this.Refresh();
            drawwater(mygraphics);
        }

        private void checkBoxSoluteVis_CheckedChanged(object sender, EventArgs e) // MDW_V2
        {

            tracergb_initialising = true;

            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            comboBox4.Items.Clear();
            //comboBox2.Items.Add(" ");
            //comboBox3.Items.Add(" ");
            //comboBox4.Items.Add(" ");
            comboBox2.Items.Add("0 : [none]"); // MDW_V2
            comboBox3.Items.Add("0 : [none]"); // MDW_V2
            comboBox4.Items.Add("0 : [none]"); // MDW_V2

            if (checkBoxSoluteVis.Checked == true)
            {
                if (checkBox12.Checked == true)
                {
                    checkBox12.Checked = false;
                }

                // Add rain zones to the lists
                for (int z = 0; z < nSolutes; z++) // MDW_V2 updated index and string below
                {
                    comboBox2.Items.Add(Convert.ToString(z + 1) + " : solute " + Convert.ToString(z + 1));
                    comboBox3.Items.Add(Convert.ToString(z + 1) + " : solute " + Convert.ToString(z + 1));
                    comboBox4.Items.Add(Convert.ToString(z + 1) + " : solute " + Convert.ToString(z + 1));
                }
                // Assign selected layer for first time
                if (tracesolute_rgb[0] == -1)
                {
                    comboBox2.SelectedIndex = 1; tracesolute_rgb[0] = 1;
                    if (nSolutes > 1) { comboBox3.SelectedIndex = 2; tracesolute_rgb[1] = 2; } // does not default to zero if not loaded
                    else { comboBox3.SelectedIndex = 0; tracesolute_rgb[1] = 0; }
                    if (nSolutes > 2) { comboBox4.SelectedIndex = 3; tracesolute_rgb[2] = 3; } // does not default to zero if not loaded
                    else { comboBox4.SelectedIndex = 0; tracesolute_rgb[2] = 0; }

                }
                // or to the one previously selected
                else
                {
                    comboBox2.SelectedIndex = tracesolute_rgb[0];
                    comboBox3.SelectedIndex = tracesolute_rgb[1];
                    comboBox4.SelectedIndex = tracesolute_rgb[2];
                }
            }
            else
            {
                if (checkBox12.Checked == true)
                {
                    return; // Do nothing: checkBox12_CheckedChanged will handle this
                }

                for (int z = 0; z < nSources; z++) // MDW_V2 updated to zero index
                {
                    // MDW_V2 - source selector box string generation, updated indices
                    string comboText = "";
                    if (z == 0) // tide source
                    {
                        if (checkBox3.Checked == true)
                        {
                            comboText = Convert.ToString(z + 1) + " : Stage";
                        }
                        else
                        {
                            comboText = Convert.ToString(z + 1) + " : Stage [not active]";
                        }
                    }
                    else if (z == 1) // rain source
                    {
                        if (catchment_mode_box.Checked == true)
                        {
                            comboText = Convert.ToString(z + 1) + " : Rain";
                        }
                        else
                        {
                            comboText = Convert.ToString(z + 1) + " : Rain [not active]";
                        }
                    }
                    else if (z >= 2) // hydro sources
                    {
                        comboText = Convert.ToString(z + 1) + " : " + inputfilenames[z - sourceIndexAddition];
                    }
                    comboBox2.Items.Add(comboText);
                    comboBox3.Items.Add(comboText);
                    comboBox4.Items.Add(comboText);
                }
                // Assign selected layer to the one previously selected
                comboBox2.SelectedIndex = trace_rgb[0];
                comboBox3.SelectedIndex = trace_rgb[1];
                comboBox4.SelectedIndex = trace_rgb[2];
            }
            tracergb_initialising = false;
            this.Refresh();
            drawwater(mygraphics);

        }

        private void OilTab_checkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (OilTab_checkBox.Checked == true)
            {
                isOilSimulation = true;
                //checkBox11.Enabled = true; //MDW_V2 changed from visible, so it shows greyed out by default
                //textBox20.Enabled = true;  //
                //checkBoxSoluteTracer.Enabled = true; //MDW_V2
                //labelSoluteNumber.Enabled = true;
            }
            else
            {
                isOilSimulation = false;
                //checkBox11.Enabled = false; //MDW_V2 changed from visible, so it shows greyed out by default
                //textBox20.Enabled = false;  //
                //checkBoxSoluteTracer.Enabled = false; //MDW_V2
                //labelSoluteNumber.Enabled = false;
            }
        }

        private void TempTab_checkBox_CheckedChanged(object sender, EventArgs e) // TEMP_V1
        {
            isSimulateTemperature = TempTab_checkBox.Checked;
        }
        private void TempTab_scheme_CheckedChanged(object sender, EventArgs e) // TEMP_V1
        {
            useSimplifiedTempScheme = TempTab_radio_simplifiedscheme.Checked;
        }

        private void TempTab_checkBox_hecraslongwave_CheckedChanged(object sender, EventArgs e) // TEMP_V1
        {
            useHecRasLongwave = TempTab_checkBox_hecraslongwave.Checked;
        }

        private void TempTab_checkBox_hecrasalbedo_CheckedChanged(object sender, EventArgs e) // TEMP_V1
        {
            useHecRasAlbedo = TempTab_checkBox_hecrasalbedo.Checked;
        }

        private void TempTab_textBox_thermalinterval_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_thermalinterval.Text, out thermal_update_interval);
        }

        private void TempTab_textBox_mettimestep_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_mettimestep.Text, out met_data_time_step);
        }

        private void TempTab_textBox_latitude_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_latitude.Text, out siteLatitude);
        }

        private void TempTab_textBox_longitude_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_longitude.Text, out siteLongitude);
        }

        private void TempTab_textBox_timezone_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_timezone.Text, out siteTimeZone);
        }

        private void TempTab_textBox_elevation_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_elevation.Text, out siteElevation);
        }

        private void TempTab_textBox_windheight_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_windheight.Text, out windMeasurementHeight);
        }
        private void TempTab_textBox_startdate_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            DateTime parsed;
            if (DateTime.TryParse(TempTab_textBox_startdate.Text, out parsed))
            {
                simulationStartDateTime = parsed;
            }
        }

        private void TempTab_textBox_initialtemp_TextChanged(object sender, EventArgs e) // TEMP_V1
        {
            double.TryParse(TempTab_textBox_initialtemp.Text, out waterTempInitialValue);
        }
        private void menuItem10_Click(object sender, EventArgs e)
        {
            menuItem10.Checked = (!menuItem10.Checked);
            if (menuItem10.Checked == true)
            {
                comboBox1.Items.Add("water source tracer");
            }
            else
            {
                comboBox1.Items.Remove("water source tracer");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }

        //OIL_V1_MDW
        private void menuItemOilVisualisation_Click(object sender, EventArgs e)
        {
            menuItemOilVisualisation.Checked = (!menuItemOilVisualisation.Checked);
            if (menuItemOilVisualisation.Checked == true)
            {
                comboBox1.Items.Add("oil spill");
            }
            else
            {
                comboBox1.Items.Remove("oil spill");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }

        //OIL_V1_PDF
        private void menuItemOilconcVisualisation_Click(object sender, EventArgs e)
        {
            menuItemOilconcVisualisation.Checked = (!menuItemOilconcVisualisation.Checked);
            if (menuItemOilconcVisualisation.Checked == true)
            {
                comboBox1.Items.Add("oil concentration");
            }
            else
            {
                comboBox1.Items.Remove("oil concentration");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }

        // TEMP_V1
        private void menuItemWaterTempVisualisation_Click(object sender, EventArgs e)
        {
            menuItemWaterTempVisualisation.Checked = (!menuItemWaterTempVisualisation.Checked);
            if (menuItemWaterTempVisualisation.Checked == true)
            {
                comboBox1.Items.Add("water temperature");
            }
            else
            {
                comboBox1.Items.Remove("water temperature");
            }
            popComboBox1();
            updateClick = 1;
            this.Refresh();
            drawwater(mygraphics);
        }
    }




    class point
    {
        //lat long variables
        public double xcoord;
        public double ycoord;
        public int UTMzone;
        public bool south;
        double transParallelX = 446.448;
        double transParallelY = -125.157;
        double transParallelZ = 542.060;
        double scaleChange = -20.4894 * 0.000001;
        double rotX = (0.1502 / 3600) * (Math.PI / 180);
        double rotY = (0.2470 / 3600) * (Math.PI / 180);
        double rotZ = (0.8421 / 3600) * (Math.PI / 180);
        double a = 6377563.396; //airy 1830 semi-major axis
        double b = 6356256.910; //airy 1830 semi-minor axis
        double a2 = 6378137.000;
        double b2 = 6356752.3142;
        double eSquared = 0;
        double eSquared2 = 0;
        double nO = -100000;//northing of true origin
        double eO = 400000;//easting of true origin
        double fO = 0.9996012717;//scale factor
        double latTrue = 49.0 * (Math.PI / 180.0);//latitude of true origin
        double longTrue = -2.0 * (Math.PI / 180.0);//longitude of true origin
        double psiHash, MBig, v, v2, v3, nLittle, rho, nSquare = 0;
        double vii, viii, ix, Tx2, xi, xii, xiia = 0;
        double helmertX, helmertY, helmertZ, cartX, cartY, cartZ;
        double Height2 = 0;
        double finalLati, finalLongi, latiRad, longiRad = 0;
        double rootXYSqr = 0;
        double PHI1, PHI2, PHI = 0;

        public point(double theXcoord, double theYcoord)//constructor
        {
            this.xcoord = theXcoord;
            this.ycoord = theYcoord;
        }

        public void transformPoint()//british os to lat long
        {
            eSquared = (Math.Pow(a, 2) - Math.Pow(b, 2)) / Math.Pow(a, 2);
            Height2 = 0;

            //OSGB36 easting and northing to OSGB36 latitude and longitude (lower left corner of DTM)
            psiHash = ((this.ycoord - nO) / (a * fO)) + latTrue;
            nLittle = (a - b) / (a + b);
            MBig = b * fO * (((1 + nLittle + ((5.0 / 4.0) * Math.Pow(nLittle, 2)) + ((5.0 / 4.0) * Math.Pow(nLittle, 3))) * (psiHash -
                     latTrue))
               - (((3.0 * nLittle) + (3.0 * Math.Pow(nLittle, 2)) + ((21.0 / 8.0) * Math.Pow(nLittle, 3))) *
                     Math.Sin(psiHash - latTrue) * Math.Cos(psiHash + latTrue))
               + (((15.0 / 8.0 * Math.Pow(nLittle, 2)) + (15.0 / 8.0 * Math.Pow(nLittle, 3))) * Math.Sin(2.0 * (psiHash - latTrue)) * Math.Cos(2.0 * (psiHash + latTrue)))
               - ((35.0 / 24.0 * Math.Pow(nLittle, 3)) * Math.Sin(3.0 * (psiHash - latTrue)) * Math.Cos(3.0 * (psiHash + latTrue))));
            if (Math.Abs((this.ycoord - nO - MBig)) >= 0.01)
            {
                while (Math.Abs((this.ycoord - nO - MBig)) >= 0.01)
                {
                    psiHash = ((this.ycoord - nO - MBig) / (a * fO)) + psiHash;
                    MBig = b * fO * (((1 + nLittle + ((5.0 / 4.0) * Math.Pow(nLittle, 2)) + ((5.0 / 4.0) * Math.Pow(nLittle, 3))) * (psiHash -
                       latTrue))
                 - (((3.0 * nLittle) + (3.0 * Math.Pow(nLittle, 2)) + ((21.0 / 8.0) * Math.Pow(nLittle, 3))) *
                       Math.Sin(psiHash - latTrue) * Math.Cos(psiHash + latTrue))
                 + (((15.0 / 8.0 * Math.Pow(nLittle, 2)) + (15.0 / 8.0 * Math.Pow(nLittle, 3))) * Math.Sin(2.0 * (psiHash - latTrue)) * Math.Cos(2.0 * (psiHash + latTrue)))
                 - ((35.0 / 24.0 * Math.Pow(nLittle, 3)) * Math.Sin(3.0 * (psiHash - latTrue)) * Math.Cos(3.0 * (psiHash + latTrue))));
                }
            }
            v = a * fO * Math.Pow(1 - eSquared * Math.Pow(Math.Sin(psiHash), 2), -.5);
            rho = a * fO * (1 - eSquared) * Math.Pow(1.0 - eSquared * Math.Pow(Math.Sin(psiHash), 2), -1.5);
            nSquare = v / rho - 1.0;
            vii = (Math.Tan(psiHash)) / (2.0 * rho * v);
            viii = ((Math.Tan(psiHash)) / (24.0 * rho * Math.Pow(v, 3))) * (5 + 3.0 * Math.Pow(Math.Tan(psiHash), 2) + nSquare - 9.0 * (Math.Pow(Math.Tan(psiHash), 2) * nSquare));
            ix = (Math.Tan(psiHash) / ((720.0 * rho * Math.Pow(v, 5)))) * (61 + 90.0 * Math.Pow(Math.Tan(psiHash), 2) + 45.0 * Math.Pow(Math.Tan(psiHash), 4));
            Tx2 = (1.0 / Math.Cos(psiHash)) / v;
            xi = (1.0 / Math.Cos(psiHash)) / (6.0 * Math.Pow(v, 3)) * ((v / rho) + (2.0 * Math.Pow(Math.Tan(psiHash), 2)));
            xii = (1.0 / Math.Cos(psiHash)) / (120.0 * Math.Pow(v, 5)) * (5.0 + (28.0 * Math.Pow(Math.Tan(psiHash), 2)) + (24.0 * Math.Pow(Math.Tan(psiHash), 4)));
            xiia = ((1.0 / Math.Cos(psiHash)) / (5040.0 * Math.Pow(v, 7))) * (61.0 + (662.0 * Math.Pow(Math.Tan(psiHash), 2)) + (1320.0 * Math.Pow(Math.Tan(psiHash), 4)) + (720.0 * Math.Pow(Math.Tan(psiHash), 6)));
            latiRad = psiHash - (vii * Math.Pow((this.xcoord - eO), 2)) + (viii * Math.Pow((this.xcoord - eO), 4)) - (ix * Math.Pow((this.xcoord - eO), 6));
            longiRad = longTrue + (Tx2 * (this.xcoord - eO)) - (xi * (Math.Pow((this.xcoord - eO), 3))) + (xii * (Math.Pow((this.xcoord - eO), 5))) - (xiia * (Math.Pow((this.xcoord - eO), 7)));
            //Console.WriteLine(latiRad * (180 / Math.PI));
            //Console.WriteLine(longiRad * (180 / Math.PI));
            //OSGB36 Latitude Longitude Height to OSGB36 Cartesian XYZ
            v2 = a / (Math.Sqrt(1 - (eSquared * ((Math.Pow(Math.Sin(latiRad), 2))))));
            cartX = (v2 + Height2) * Math.Cos(latiRad) * Math.Cos(longiRad);
            cartY = (v2 + Height2) * (Math.Cos(latiRad) * Math.Sin(longiRad));
            cartZ = ((v2 * (1 - eSquared)) + Height2) * Math.Sin(latiRad);
            //Console.WriteLine();
            //Console.WriteLine(cartX);
            //Console.WriteLine(cartY);
            //Helmert Datum Transformation (OSGB36 to WGS84)
            helmertX = cartX + (cartX * scaleChange) - (cartY * rotZ) + (cartZ * rotY) + transParallelX;
            helmertY = (cartX * rotZ) + cartY + (cartY * scaleChange) - (cartZ * rotX) + transParallelY;
            helmertZ = (-1 * cartX * rotY) + (cartY * rotX) + cartZ + (cartZ * scaleChange) + transParallelZ;
            //Console.WriteLine();
            //Console.WriteLine(helmertX);
            //Console.WriteLine(helmertY);
            //WGS84 Cartesian XYZ to WGS84 Latitude, longitude and Ellipsoidal height
            rootXYSqr = Math.Sqrt((Math.Pow(helmertX, 2)) + (Math.Pow(helmertY, 2)));
            eSquared2 = (Math.Pow(a2, 2) - Math.Pow(b2, 2)) / Math.Pow(a2, 2);
            PHI1 = Math.Atan(helmertZ / (rootXYSqr * (1 - eSquared2)));
            v3 = a2 / (Math.Sqrt(1.0 - (eSquared2 * ((Math.Pow(Math.Sin(PHI1), 2))))));
            PHI2 = Math.Atan((helmertZ + (eSquared2 * v3 * (Math.Sin(PHI1)))) / rootXYSqr);
            while (Math.Abs(PHI1 - PHI2) > 0.000000001)
            {
                PHI1 = PHI2;
                v3 = a2 / (Math.Sqrt(1 - (eSquared2 * ((Math.Pow(Math.Sin(PHI1), 2))))));
                PHI2 = Math.Atan((helmertZ + (eSquared2 * v3 * (Math.Sin(PHI1)))) / rootXYSqr);
            }
            PHI = PHI2;
            finalLati = PHI * (180.0 / Math.PI);
            finalLongi = (Math.Atan(helmertY / helmertX)) * (180.0 / Math.PI);
            this.xcoord = finalLongi;
            this.ycoord = finalLati;
        }

        public void transformUTMPoint()
        {
            //transforms coordinates in UTM WGS84 to lat long
            //requires x, y, zone and north/south
            //the code in this function was found at http://home.hiwaay.net/~taylorc/toolbox/geography/geoutm.html
            //made by Chuck Taylor
            //tested in xls for points in Poland, Turkey and South Africa

            // The code first calculates TM coordinates from UTM coordinates
            // Then calculates corresponding latitude and longitude in radians
            // Before converting back to degrees

            // first version ArT 12-06-09

            double footpointlatitude = 0;
            double UTMscalefactor = 0.9996;
            double centralmeridian_deg = 0;
            double centralmeridian_rad = 0;
            double y_ = 0;
            double WGS84_sm_a = 6378137;
            double WGS84_sm_b = 6356752.314;
            double n = (WGS84_sm_a - WGS84_sm_b) / (WGS84_sm_a + WGS84_sm_b);

            this.xcoord = (this.xcoord - 500000) / UTMscalefactor;
            if (this.south) this.ycoord = (this.ycoord - 10000000) / UTMscalefactor;
            else this.ycoord /= UTMscalefactor;

            centralmeridian_deg = -183 + (this.UTMzone * 6);
            centralmeridian_rad = centralmeridian_deg / 180 * Math.PI;

            double alpha = (((WGS84_sm_a + WGS84_sm_b) / 2) * (1 + Math.Pow(n, 2) / 4) + (Math.Pow(n, 4) / 64));
            double beta = (3 * n / 2) + (-27 * Math.Pow(n, 3) / 32) + (269 * Math.Pow(n, 5) / 512);
            double gamma = (21 * Math.Pow(n, 2) / 16) + (-55 * Math.Pow(n, 4) / 32);
            double delta = (151 * Math.Pow(n, 3) / 96) + (-417 * Math.Pow(n, 5) / 128);
            double epsilon = (1097 * Math.Pow(n, 4) / 512);

            y_ = this.ycoord / (alpha);
            footpointlatitude = y_ + (beta * Math.Sin(2 * y_)) + (gamma * Math.Sin(4 * y_)) + (delta * Math.Sin(6 * y_)) + (epsilon * Math.Sin(8 * y_));

            double ep2 = (Math.Pow(WGS84_sm_a, 2) - Math.Pow(WGS84_sm_b, 2)) / Math.Pow(WGS84_sm_b, 2);
            double cf = Math.Cos(footpointlatitude);
            double nuf2 = ep2 * Math.Pow(cf, 2);
            double nf = Math.Pow(WGS84_sm_a, 2) / (WGS84_sm_b * Math.Sqrt(1 + nuf2));

            double tf = Math.Tan(footpointlatitude);
            double tf2 = Math.Pow(tf, 2);
            double tf4 = Math.Pow(tf, 4);

            double x1frac = 1 / (1 * Math.Pow(nf, 1) * cf);
            double x2frac = tf / (2 * Math.Pow(nf, 2));
            double x3frac = 1 / (6 * Math.Pow(nf, 3) * cf);
            double x4frac = tf / (24 * Math.Pow(nf, 4));
            double x5frac = 1 / (120 * Math.Pow(nf, 5) * cf);
            double x6frac = tf / (720 * Math.Pow(nf, 6));
            double x7frac = 1 / (5040 * Math.Pow(nf, 7) * cf);
            double x8frac = tf / (40320 * Math.Pow(nf, 8));

            double x2poly = -1 - nuf2;
            double x3poly = -1 - nuf2 - (2 * tf2);
            double x4poly = 5 + 3 * tf2 + 6 * nuf2 - 6 * tf2 * nuf2 - 3 * Math.Pow(nuf2, 2) - 9 * tf2 * nuf2 * nuf2;
            double x5poly = 5 + 28 * tf2 + 24 * tf4 + 6 * nuf2 + 8 * tf2 * nuf2;
            double x6poly = -61 - 90 * tf2 - 45 * tf4 - 107 * nuf2 + 162 * tf2 * nuf2;
            double x7poly = -61 - 662 * tf2 - 1320 * tf4 - 720 * tf4 * tf2;
            double x8poly = 1385 + 3633 * tf2 + 4095 * tf4 + 1575 * tf4 * tf2;

            double latitude = footpointlatitude
                            + x2frac * x2poly * Math.Pow(this.xcoord, 2)
                            + x4frac * x4poly * Math.Pow(this.xcoord, 4)
                            + x6frac * x6poly * Math.Pow(this.xcoord, 6)
                            + x8frac * x8poly * Math.Pow(this.xcoord, 8);
            double longitude = centralmeridian_rad
                            + x1frac * 1 * Math.Pow(this.xcoord, 1)
                            + x3frac * x3poly * Math.Pow(this.xcoord, 3)
                            + x5frac * x5poly * Math.Pow(this.xcoord, 5)
                            + x7frac * x7poly * Math.Pow(this.xcoord, 7);

            this.ycoord = latitude / Math.PI * 180;
            this.xcoord = longitude / Math.PI * 180;
        }

    }



}

