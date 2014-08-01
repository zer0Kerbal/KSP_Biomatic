using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.IO;
using Biomatic.Extensions;
using System.Reflection;

namespace Biomatic
{
    class Biomatic : PartModule
    {
        private static Rect windowPos = new Rect();

        private static bool UseToolbar = false;
        private static bool toolbarShowSettings = false;
        public static bool ToolbarShowSettings
        {
            get { return toolbarShowSettings; }
            set
            {
                toolbarShowSettings = value; 
                sizechange = true;
            }
        }
        
        private static bool useStockToolBar = true;
        public static bool UseStockToolBar
        {
            get { return useStockToolBar; }
            set { useStockToolBar = value; }
        }
        
        private static bool systemOn = true;
        public static bool SystemOn
        {
            get { return systemOn; }
            set { systemOn = value; }
        }

        private static ToolbarButtonWrapper toolbarButton = null;
        private bool newInstance = true;

        // resize window? - prevents blinking when buttons clicked
        private static bool sizechange = true;

        // rate of visual and audio output
        private int skip = 0;

        private static bool deWarp = false;
        private static bool includeAlt = false;
        private static bool showHistory = false;
        private static bool showDescription = false;

        private static List<string> listIgnore = null;

        private bool isPowered = true;
        private bool wasPowered = true;

        private bool lostToStaging = false;

        private GUIStyle styleTextArea = null;
        private GUIStyle styleButton = null;
        private GUIStyle styleValue = null;
        private GUIStyle styleToggle = null;

        private float fixedwidth = 255f;
        private float margin = 20f;

        private static bool prevConditionalShow = false;
        private static bool ConditionalShow = true;

        private BiomeSituation biome = new BiomeSituation();
        private BiomeSituation prevBiome = new BiomeSituation();

        private BiomeSituation[] historyArray = null;

        EventType prevEventType;

        private int numParts = -1;
        private int stage = -1;

        private Vessel ActiveVessel;

        public override void OnStart(StartState state)
        {
            //print("###Biomatic OnStart");
            if (!useStockToolBar) // blizzy
            {
                try
                {
                    //print("###Toolbar init");
                    toolbarButton = new ToolbarButtonWrapper("Biomatic", "toolbarButton");
                    RefreshBlizzyButton();
                    toolbarButton.ToolTip = "Biomatic settings";
                    toolbarButton.Visible = true;
                    toolbarButton.AddButtonClickHandler((e) =>
                    {
                        //print("### prox toolbar button clicked");
                        toolbarShowSettings = !toolbarShowSettings;
                        RefreshBlizzyButton();
                        sizechange = true;
                    });
                }
                catch (Exception ex)
                {
                    //print("###Exception on blizzy toolbar init, msg = " + ex.Message);
                }
                UseToolbar = true;
            }
            else // stock
            {
                UseToolbar = true;
            }

            if (state != StartState.Editor)
            {
                RenderingManager.AddToPostDrawQueue(0, OnDraw);

                if (listIgnore == null)
                {
                    listIgnore = new List<string>();
                }

                historyArray = new BiomeSituation[5];
                sizechange = true;
            }
        }

        private bool RefreshStockButton()
        {
            bool result = false;

            //print("###RefreshStockButton");

            if (useStockToolBar)
            {
                //print("###RefreshStockButton, using stock tb");
                StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                if (stb != null)
                {
                    //print("###RefreshStockButton, got stock tb");
                    result = true; 
                    stb.ButtonNeeded = true; 
                    stb.CreateButton();
                    //print("###RefreshStockButton: stb.CreateButton() called, result " + stb.ButtonNeeded.ToString());
                    if (!stb.ButtonNeeded)
                    {
                        result = false;
                        windowPos.height = 20;
                        lostToStaging = true;
                        //print("###RefreshStockButton, set lostToStaging = true");
                    }
                }
            }

            return result;
        }

        private bool NeedsStockButton()
        { 
            bool result = false;

            if (useStockToolBar)
            {
                StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                if (stb != null)
                {
                    result = stb.ButtonNeeded;
                }
            }

            return result;
        }

        private bool RefreshBlizzyButton()
        {
            bool relevant = IsRelevant();
            toolbarButton.Visible = relevant;

            if (relevant)
            {
                string path = "Biomatic/ToolbarIcons/Biom";
                path += toolbarShowSettings ? "Grey" : "Colour";

                if (!systemOn)
                {
                    path += "X";
                }

                toolbarButton.TexturePath = path;
            }
            else
            {
                lostToStaging = true;
            }

            return relevant;
        }

        public override void OnSave(ConfigNode node)
        {
            //print("###OnSave");
            PluginConfiguration config = PluginConfiguration.CreateForType<Biomatic>();

            config.SetValue("Window Position", windowPos);

            config.SetValue("List items", listIgnore.Count);
            List<string>.Enumerator en = listIgnore.GetEnumerator();
            int count = 0;
            while (en.MoveNext())
            {
                config.SetValue("Item" + count.ToString(), en.Current);
                count++;
            }
            en.Dispose();

            config.SetValue("DeWarp", deWarp);
            config.SetValue("Use altitude", includeAlt);
            config.SetValue("Show description", showDescription);
            config.SetValue("Toolbar", useStockToolBar ? "stock": "blizzy");

            config.save();
        }

        public override void OnLoad(ConfigNode node)
        {
            //print("###OnLoad");
            PluginConfiguration config = PluginConfiguration.CreateForType<Biomatic>();

            config.load();

            try 
            { 
                windowPos = config.GetValue<Rect>("Window Position");

                if (listIgnore == null)
                {
                    listIgnore = new List<string>();
                }
                else
                {
                    listIgnore.Clear();
                }

                int count = config.GetValue<int>("List items");
                for (int i = 0; i < count; i++)
                {
                    listIgnore.Add(config.GetValue<string>("Item" + i.ToString()));
                }

                deWarp = config.GetValue<bool>("DeWarp");
                includeAlt = config.GetValue<bool>("Use altitude");
                showDescription = config.GetValue<bool>("Show description");
                string s = config.GetValue<string>("Toolbar");
                s = s.ToLower();
                useStockToolBar = !(s.Contains("blizzy"));
            }
            catch (Exception ex)
            { 
                // likely a line is missing. 
            }

            windowPos.width = fixedwidth;
        }

        private void OnDraw()
        {
            //print("###OnDraw - event = " + Event.current.type.ToString());
            if (Event.current.type == prevEventType && (prevEventType == EventType.Repaint || prevEventType == EventType.Layout))
            {
                //print("###OnDraw - returning, identical event type to prev");
                return;
            }

            ActiveVessel = FlightGlobals.ActiveVessel;

            //print("###OnDraw");
            if (ActiveVessel != null)
            {
                // this takes account of vessels splitting (when undocking), Kerbals going on EVA, etc.
                if (newInstance || (useStockToolBar && (ActiveVessel.parts.Count != numParts || ActiveVessel.currentStage != stage)))
                {
                    numParts = ActiveVessel.parts.Count;
                    stage = ActiveVessel.currentStage;
                    //print("###num parts = " + numParts.ToString() + ", stage = " + stage.ToString());
                    
                    newInstance = false;
                    lostToStaging = false;
                    if (useStockToolBar)
                    {
                        if (!RefreshStockButton())
                        {
                            return;
                        }
                    }
                    else
                    {
                        RefreshBlizzyButton();
                    }
                }

                if (RightConditionsToDraw())
                {
                    //print("###OnDraw - drawing");

                    styleTextArea = new GUIStyle(GUI.skin.textArea);
                    styleTextArea.normal.textColor = Color.green;
                    styleTextArea.alignment = TextAnchor.MiddleCenter;
                    styleTextArea.fixedWidth = fixedwidth - 40;

                    styleButton = new GUIStyle(GUI.skin.button);
                    styleButton.normal.textColor = styleButton.hover.textColor = styleButton.focused.textColor = styleButton.active.textColor = Color.white;
                    styleButton.active.textColor = Color.green;
                    styleButton.padding = new RectOffset(0, 0, 0, 0);

                    styleToggle = new GUIStyle(GUI.skin.toggle);

                    styleValue = new GUIStyle(GUI.skin.label);
                    styleValue.normal.textColor = Color.white;
                    styleValue.alignment = TextAnchor.MiddleCenter;

                    if (ConditionalShow != prevConditionalShow)
                    {
                        sizechange = true;
                        skip = 0;
                    }

                    if (sizechange)
                    {
                        windowPos.yMax = windowPos.yMin + 20;
                        sizechange = false;
                    }

                    //print("###event type:" + Event.current.type.ToString());

                    windowPos = GUILayout.Window(this.ClassID, windowPos, OnWindow, ConditionalShow ? "Biomatic" : "Biomatic settings", GUILayout.Width(fixedwidth));
                    windowPos.width = fixedwidth;

                    if (windowPos.x == 0 && windowPos.y == 0)
                    {
                        windowPos = windowPos.CentreScreen();
                    }
                }
            }
        }

        private bool RightConditionsToDraw()
        {
            //print("###RightConditionsToDraw");
            bool retval = true;

            if(!part.IsPrimary(ActiveVessel.parts, ClassID))
            {
                //print("###Not processing - multiple part, clsID = " + this.ClassID);
                return false; // this is such a hack
            }

            if (lostToStaging)
            {
                //print("###Not processing - lost to staging");
                prevConditionalShow = ConditionalShow = false;
                return false;
            }

            if (!systemOn)
            { 
                //print("###Not processing - manually deactivated");
                retval = false;
            }

            prevConditionalShow = ConditionalShow;
            ConditionalShow = retval;
            return ConditionalShow || (UseToolbar && toolbarShowSettings);
         }

        private void OnWindow(int windowID)
        {
            //print("###OnWindow");
            try
            {
                DoBiomaticContent();
            }
            catch (Exception ex)
            {
                //print("###DoBiomaticContent exeption - " + ex.Message);
            }
            GUI.DragWindow();
        }

        private void DoBiomaticContent()
        {
            if (Event.current.type == EventType.repaint && ConditionalShow)
            {
                isPowered = IsPowered();
                if (isPowered != wasPowered)
                {
                    sizechange = true;
                    wasPowered = isPowered;
                }

                if (isPowered)
                {
                    skip--;

                    if (skip <= 0)
                    {
                        skip = 10;

                        biome = GetBiomeSituation();

                        if (!prevBiome.IsSameAs(biome, includeAlt))
                        {
                            //print("DIFFERENT: " + prevBiome.GetText(includeAlt) + " : " + biome.GetText(includeAlt));

                            prevBiome = biome;

                            if (!BiomeInList(biome.GetText(includeAlt)))
                            {
                                if (deWarp)
                                {
                                    TimeWarp.SetRate(0, false);
                                }
                            }

                            AddToArray(biome);
                        }
                        else
                        {
                            //print("SAME: " + prevBiome.GetText(includeAlt) + " : " + biome.GetText(includeAlt));
                        }
                    }
                }
                else
                {
                    showDescription = false;
                    showHistory = false;
                }
            }

            prevEventType = Event.current.type;

            ShowGraphicalIndicator();

            if (isPowered)
            {
                ShowSettings();
            }
        }

        private BiomeSituation GetBiomeSituation()
        {
            return new BiomeSituation(GetBiomeString(), GetSituationString(), ActiveVessel.mainBody.name);
        }

        private void AddToArray(BiomeSituation biome)
        {
            for (int i = 4; i > 0; i--)
            {
                historyArray[i] = historyArray[i - 1];
            }
            historyArray[0] = biome;
        }

        private void ShowGraphicalIndicator()
        {
            if (systemOn && ConditionalShow)
            {
                // show description
                if (showDescription)
                {
                    styleValue.normal.textColor = styleValue.focused.textColor = Color.green;
                    GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                    GUILayout.Label(ActiveVessel.RevealSituationString(), styleValue);
                    GUILayout.EndHorizontal();
                }

                // CURRENT BIOME
                DisplayLine(historyArray[0]);

                if (isPowered && showHistory)
                {
                    ShowHistoricBiomeSits();
                }
            }
        }

        private void ShowHistoricBiomeSits()
        {
            styleButton.normal.textColor = Color.red;
            for (int i = 1; i < 5; i++)
            {
                if (historyArray[i] != null)
                {
                    DisplayLine(historyArray[i]);
                }
                else
                {
                    break;
                }
            }
        }

        private void DisplayLine(BiomeSituation bs)
        { 
            GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
            if (isPowered)
            {
                styleTextArea.normal.textColor = Color.green;
                string text = GetOutputString(bs);
                GUILayout.Label(text, styleTextArea);

                styleButton.normal.textColor = (text.Contains("\u2713") ? Color.red : Color.green);
                if (GUILayout.Button(".", styleButton, GUILayout.ExpandWidth(true)))
                {
                    if (text.Contains("\u2713"))
                    {
                        RemoveCurrentBiomeFromList(bs);
                    }
                    else
                    {
                        AddCurrentBiomeToList(bs);
                    }
                }
                styleButton.normal.textColor = styleButton.hover.textColor = styleButton.focused.textColor = styleButton.active.textColor = Color.white;
                styleButton.active.textColor = Color.green;
            }
            else
            { 
                styleTextArea.normal.textColor = Color.grey;
                GUILayout.Label("----unpowered----", styleTextArea);
            }
            GUILayout.EndHorizontal();
        }

        private string GetFullBiomeName(string bio, bool useAlt)
        {
            string result = ActiveVessel.mainBody.name + "." + bio;

            if (useAlt)
            {
                result = GetSituationString() + "." + result;
            }

            return result;
        }

        private string GetSituationString()
        {
            string result = "";
            ExperimentSituations sit = ScienceUtil.GetExperimentSituation(ActiveVessel);
            switch (sit)
            { 
                case ExperimentSituations.FlyingHigh:
                    result = "High flight";
                    break;
                case ExperimentSituations.FlyingLow:
                    result = "Low flight";
                    break;
                case ExperimentSituations.InSpaceHigh:
                    result = "High above";
                    break;
                case ExperimentSituations.InSpaceLow:
                    result = "Just above";
                    break;
                case ExperimentSituations.SrfLanded:
                    result = "Landed";
                    break;
                case ExperimentSituations.SrfSplashed:
                    result = "Splashed";
                    break;
            }

            return result;
        }

        // show settings buttons / fields
        private void ShowSettings()
        { 
            if (UseToolbar && toolbarShowSettings)
            {
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                styleButton.normal.textColor = Color.white;
                if (GUILayout.Button("Remove " + ActiveVessel.mainBody.name + " biomes from list", styleButton, GUILayout.ExpandWidth(true)))
                {
                    RemoveCurrentBody();
                }
                GUILayout.EndHorizontal();

                // de-warp
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                deWarp = GUILayout.Toggle(deWarp, " De-warp", styleToggle, null);

                //show hist
                bool oldShowHistory = showHistory;
                showHistory = GUILayout.Toggle(showHistory, " Show recent", styleToggle, null);
                GUILayout.EndHorizontal();

                // use altitude
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                includeAlt = GUILayout.Toggle(includeAlt, " Altitude", styleToggle, null);

                // show description
                bool oldShowDescription = showDescription;
                showDescription = GUILayout.Toggle(showDescription, " Description", styleToggle, null);
                GUILayout.EndHorizontal();
                if (showDescription != oldShowDescription || oldShowHistory != showHistory)
                {
                    sizechange = true;
                }

                styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = systemOn ? Color.red: Color.green;
                styleValue.normal.textColor = Color.white;

                // On / Off switch
                GUILayout.BeginHorizontal(GUILayout.Width(fixedwidth - margin));
                GUILayout.Label("Biomatic ", styleValue);
                styleValue.normal.textColor = systemOn ? Color.green: Color.red;
                GUILayout.Label(systemOn ? "ON ": "OFF ", styleValue);
                if (GUILayout.Button(systemOn ? "Switch off": "Switch on", styleButton, GUILayout.ExpandWidth(true)))
                {
                    systemOn = !systemOn;
                    if (!useStockToolBar)
                    {
                        RefreshBlizzyButton();
                    }
                    else 
                    {
                        // here be twiddles
                        //RefreshStockButton();
                        //print("###ShowSettings, toggling on off");
                        StockToolbar stb = (StockToolbar)StockToolbar.FindObjectOfType(typeof(StockToolbar));
                        if (stb != null)
                        {
                            stb.RefreshButtonTexture();
                        }
                    }

                    sizechange = true;
                }
                GUILayout.EndHorizontal();

                styleValue.normal.textColor = styleValue.focused.textColor = styleValue.hover.textColor = styleValue.active.textColor = Color.white;
                styleButton.normal.textColor = styleButton.focused.textColor = styleButton.hover.textColor = styleButton.active.textColor = Color.white;
            }
        }

        public void OnDestroy()
        {
            //print("###OnDestroy - Biomatic");
            if (toolbarButton != null)
            {
                toolbarButton.Destroy();
            }
        }

        private bool IsPowered()
        {
            double electricCharge = 0;

            foreach (Part p in ActiveVessel.parts)
            {
                foreach (PartResource pr in p.Resources)
                {
                    if (pr.resourceName.Equals("ElectricCharge") && pr.flowState)
                    {
                        electricCharge += pr.amount;
                        break;
                    }
                }
            }

            return electricCharge > 0.04;
        }

        public CBAttributeMap.MapAttribute GetBiome()
		{
			CBAttributeMap.MapAttribute mapAttribute;

			try
			{
				CBAttributeMap BiomeMap = ActiveVessel.mainBody.BiomeMap;

				double lat = ActiveVessel.latitude * Math.PI / 180d;
				double lon = ActiveVessel.longitude * Math.PI / 180d;

				mapAttribute = BiomeMap.GetAtt(lat, lon);
            }
			catch (NullReferenceException)
			{
				mapAttribute = new CBAttributeMap.MapAttribute();
				mapAttribute.name = "N/A";
			}

			return mapAttribute;
		}
        
        public string GetBiomeString()
		{
            string biome_desc = "";
			CBAttributeMap.MapAttribute mapAttribute;

			try
			{
				CBAttributeMap BiomeMap = ActiveVessel.mainBody.BiomeMap;

				double lat = ActiveVessel.latitude * Math.PI / 180d;
				double lon = ActiveVessel.longitude * Math.PI / 180d;

				mapAttribute = BiomeMap.GetAtt(lat, lon);

                biome_desc = mapAttribute.name;
            }
			catch (NullReferenceException)
			{
				mapAttribute = new CBAttributeMap.MapAttribute();
				mapAttribute.name = "N/A";
			}

			return biome_desc;
		}
        
        private void AddCurrentBiomeToList(BiomeSituation bs)
        {
            string fullname = bs.GetText(true);
            if (!BiomeInList(fullname))
            {
                listIgnore.Add(fullname);
            }
        }

        private bool BiomeInList(string fullname)
        {
            bool result = false;

            List<string>.Enumerator en = listIgnore.GetEnumerator();
            while (en.MoveNext())
            {
                if (en.Current.Contains(fullname))
                {
                    result = true;
                    break;
                }
            }
            en.Dispose();

            return result;
        }

        private void RemoveCurrentBody()
        { 
            try
            {
                while (true)
                {
                    bool removed = false;
                    foreach (string s in listIgnore)
                    {
                        if (s.Contains(ActiveVessel.mainBody.name))
                        {
                            listIgnore.Remove(s);
                            removed = true;
                            break;
                        }
                    }
                    if (!removed)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                //print("###" + ex.Message);
            }
        }

        private void RemoveCurrentBiomeFromList(BiomeSituation bs)
        { 
            try
            {
                while (true)
                {
                    bool removed = false;
                    foreach (string s in listIgnore)
                    {
                        if (s.Contains(bs.Biome) && s.Contains(bs.Body) && (!includeAlt || s.Contains(bs.Situation)))
                        {
                            listIgnore.Remove(s);
                            removed = true;
                            break;
                        }
                    }
                    if (!removed)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                //print("###" + ex.Message);
            }
        }

        private string GetOutputString(BiomeSituation bs)
        {
            if (bs == null)
            {
                return "----";
            }

            string output = bs.GetDescription(includeAlt);

            if (BiomeInList(bs.GetText(includeAlt)))
            { 
                output +=" \u2713";
            }

            return output;
        }

        public void Resize()
        {
            sizechange = true;
        }

        public static bool IsRelevant()
        { 
            bool relevant = false;
            if (HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                if (FlightGlobals.ActiveVessel != null)
                {
                    List<Biomatic> bio = FlightGlobals.ActiveVessel.FindPartModulesImplementing<Biomatic>();

                    if (bio != null && bio.Count > 0)
                    {
                        relevant = true;
                    }
                }
            }
            return relevant;
        }    
    }
}
