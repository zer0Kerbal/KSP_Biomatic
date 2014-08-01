using System.IO;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace Biomatic
{
    [KSPAddon(KSPAddon.Startup.Flight, true)]
    public class StockToolbar : MonoBehaviour
    {
        private static Texture2D shownOn;
        private static Texture2D shownOff;
        private static Texture2D hiddenOn;
        private static Texture2D hiddenOff;

        private ApplicationLauncherButton stockToolbarBtn;

        private bool buttonNeeded = false;
        public bool ButtonNeeded
        {
            get { return buttonNeeded; }
            set { buttonNeeded = value; }
        }

        void Start()
        {
            print ("###Toolbar Start");
            if (Biomatic.UseStockToolBar)
            {
                Load(ref shownOn, "BiomGrey.png");
                Load(ref shownOff, "BiomGreyCross.png");
                Load(ref hiddenOn, "BiomColour.png");
                Load(ref hiddenOff, "BiomColourCross.png");

                GameEvents.onGUIApplicationLauncherReady.Add(CreateButton);
            }
            DontDestroyOnLoad(this); // twiddle - new
        }

        private void Load(ref Texture2D tex, string file)
        { 
            if (tex == null)
            {
                tex = new Texture2D(36, 36, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), file)));
            }
        }

        public void CreateButton()
        {
            //print("###CreateButton - CreateButtons called");

            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                bool relevant = false;
                if (FlightGlobals.ActiveVessel != null)
                {
                    //print("###CreateButton - Got active vessel");

                    List<Biomatic> bio = FlightGlobals.ActiveVessel.FindPartModulesImplementing<Biomatic>();

                    if (bio != null && bio.Count > 0)
                    {
                        //print("###CreateButton - has relevant part");
                        relevant = true;
                    }
                }

                if (!relevant)
                {
                    buttonNeeded = false;
                    ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
                    //print("###CreateButton - stockToolbarBtn = " + ((stockToolbarBtn == null) ? "null": "thing"));
                }
                else
                {
                    MakeButton();
                    buttonNeeded = true;
                }
            }
            else
            {
                buttonNeeded = false;
            }
        }

        private void MakeButton()
        { 
            //print("###MakeButton");

            if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }

            stockToolbarBtn = ApplicationLauncher.Instance.AddModApplication(
                BiomaticHide, BiomaticShow, null, null, null, null,
                ApplicationLauncher.AppScenes.FLIGHT, GetTexture());

            DontDestroyOnLoad(stockToolbarBtn);

            if (!Biomatic.ToolbarShowSettings)
            {
                stockToolbarBtn.SetTrue(false);
            }
            else
            {
                stockToolbarBtn.SetFalse(false);
            }
        }

        public void RefreshButtonTexture()
        {
            if (stockToolbarBtn != null)
            {
                // here be twiddles
                stockToolbarBtn.SetTexture(GetTexture());
            }
        }

        private void BiomaticHide()
        {
            if (Biomatic.ToolbarShowSettings)
            {
                Biomatic.ToolbarShowSettings = false;
                RefreshButtonTexture();
            }
        }

        private void BiomaticShow()
        {
            if (!Biomatic.ToolbarShowSettings)
            {
                Biomatic.ToolbarShowSettings = true;
                RefreshButtonTexture();
            }
        }

        private Texture2D GetTexture()
        { 
            Texture2D tex;

            if (Biomatic.SystemOn)
            {
                tex = (Biomatic.ToolbarShowSettings ? shownOn : hiddenOn);
            }
            else
            { 
                tex = (Biomatic.ToolbarShowSettings ? shownOff : hiddenOff);
            }

            return tex;
        }

        private void OnDestroy()
        {
            //print("###OnDestroy - StockToolbar");

            if (stockToolbarBtn != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(stockToolbarBtn);
            }
        }
    }
}
