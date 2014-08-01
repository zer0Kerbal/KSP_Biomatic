using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Biomatic
{
    class BiomeSituation
    {
        private string biome = "";
        public string Biome
        {
            get { return biome; }
            set { biome = value; }
        }

        private string situation = "";
        public string Situation
        {
            get { return situation; }
            set { situation = value; }
        }

        private string body = "";
        public string Body
        {
            get { return body; }
            set { body = value; }
        }

        private bool listed = false;
        public bool Listed
        {
            get { return listed; }
            set { listed = value; }
        }
        public BiomeSituation()
        { 
        
        }

        public BiomeSituation(string bio, string sit, string bod)
        {
            biome = bio;
            situation = sit;
            body = bod;
        }

        public BiomeSituation(string desc)
        {
            string[] parts = desc.Split(new char[] { '.' });
            if (parts != null && parts.Length == 3)
            {
                situation = parts[0];
                body = parts[1];
                biome = parts[2];
            }
        }

        public BiomeSituation(BiomeSituation bs)
        { 
            biome = bs.biome;
            situation = bs.situation;
            body = bs.body;
            listed = bs.listed;
        }

        public bool IsSameAs(BiomeSituation bs, bool useSituation)
        {
            if (this.body.CompareTo(bs.body) != 0)
            {
                return false;
            }

            if (this.biome.CompareTo(bs.biome) != 0)
            {
                return false;
            }

            if (useSituation && this.situation.CompareTo(bs.situation) != 0)
            {
                return false;
            }

            return true;
        }

        public string GetText(bool useSituation)
        {
            string s = body + "." + biome;

            if (useSituation)
            {
                s = situation + "." + s;
            }

            return s;
        }

        public string GetDescription(bool useSituation)
        { 
            string s = biome;

            if (useSituation)
            {
                s = situation + ", " + s;
            }

            return s;
        }
    }
}
