using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;

namespace WeThePeople
{
    public class FiefRating
    {
        public string OwnerClan { get; set; }
        public string FiefName { get; set; }
        public float Rating { get; set; }
        public string Nation { get; set; }

        public FiefRating(string owner, string name, float rating, string nation)
        {
            OwnerClan = owner;
            FiefName = name;
            Rating = rating;
            Nation = nation;
        }
    }

    public class ClanRating
    {
        public string ClanName { get; set; }
        public float AverageRating { get; set; }
        public string Nation { get; set; }

        public ClanRating(string name, float rating, string nation)
        {
            ClanName = name;
            AverageRating = rating;
            Nation = nation;
        }
    }

    public class CultureMx
    {
        public string? Name { get; set; }
        public float Militia { get; set; }
        public float Food { get; set; }
        public float Garrison { get; set; }
        public float Loyalty { get; set; }
        public float Security { get; set; }
        public float Prosperity { get; set; }
    }

    public class Find_Approval_Rating : CampaignBehaviorBase
    {
        private int _CycleWeek = 0;
        private const float Interval = 6f;

        private const float Militia_Max_Castle = 500f;
        private const float Militia_Max_Town = 700f;

        private const float Food_Max = 300f;

        private const float Garrison_Max_Castle = 300f;
        private const float Garrison_Max_Town = 600f;

        private const float Prosperity_Max_Castle = 1500f;
        private const float Prosperity_Max_Town = 10000f;

        private const float Siege_Militia_Mx = 0.4f;
        private const float Siege_Garrison_Mx = 0.3f;
        private const float Siege_Food_Mx = 0.3f;

        private List<String> Republics = new List<String>();
        private List<CultureMx> CultureMxes = new List<CultureMx>();
        private List<FiefRating> RatingsList = new List<FiefRating>();
        private List<ClanRating> ClanRatings = new List<ClanRating>();

        private List<String> RebelRisk = new List<String>();

        public static float MxRandomOffset() { return MBRandom.RandomFloatRanged(-0.05f, 0.05f); }

        public static Kingdom? GetKingdomByName(string name)
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.ToString() == name)
                {
                    return kingdom;
                }
            }
            return null;
        }

        public void LoadConfig()
        {
            // - Load main Config file.
            XDocument doc = XDocument.Load(BasePath.Name + "Modules\\WeThePeople\\ModuleData\\Config.xml");
            InformationManager.DisplayMessage(new InformationMessage($"{doc.Root}"));

            // - Grab all kingdoms listed as republics
            Republics = doc.Element("WeThePeopleConfig").Element("RepublicKingdoms").Elements("Republic").Select(x => x.Value.Trim()).ToList();

            XElement? RepublicsElement = doc.Element("WeThePeopleConfig")?.Element("RepublicKingdoms");
            if (RepublicsElement != null )
            {
                Republics = RepublicsElement.Elements("Republic").Select(x => x.Value.Trim()).ToList();
            } else
            {
                InformationManager.DisplayMessage(new InformationMessage("[ERROR]: Root or RepublicKingdoms element missing in config!", Colors.Red));
            }

            InformationManager.DisplayMessage(new InformationMessage($"{Republics.ToString()}"));

            CultureMxes = doc.Element("WeThePeopleConfig")
                             .Element("CultureMultipliers")
                             .Elements("Culture")
                             .Select(x => new CultureMx
                             {
                                 Name = x.Element("Name").Value.Trim(),
                                 Militia = float.Parse(x.Element("Militia").Value),
                                 Food = float.Parse(x.Element("Food").Value),
                                 Garrison = float.Parse(x.Element("Garrison").Value),
                                 Loyalty = float.Parse(x.Element("Loyalty").Value),
                                 Security = float.Parse(x.Element("Security").Value),
                                 Prosperity = float.Parse(x.Element("Prosperity").Value)
                             })
                             .ToList();

            XElement? CultureMElement = doc.Element("WeThePeopleConfig")?.Element("CultureMultipliers");
            if (CultureMElement != null )
            {
                CultureMxes = CultureMElement.Elements("Culture")
                    .Select(x => new CultureMx
                    {
                        Name = x.Element("Name")?.Value?.Trim() ?? "DEFAULT",
                        Militia = float.TryParse(x.Element("Militia")?.Value, out float mil) ? mil : 0.1f,
                        Garrison = float.TryParse(x.Element("Garrison")?.Value, out float gar) ? gar : -0.2f,
                        Loyalty = float.TryParse(x.Element("Loyalty")?.Value, out float loy) ? loy : 0.3f,
                        Security = float.TryParse(x.Element("Security")?.Value, out float sec) ? sec : 0.3f,
                        Prosperity = float.TryParse(x.Element("Prosperity")?.Value, out float pro) ? pro : 0.3f
                    })
                    .ToList();
            } else
            {
                InformationManager.DisplayMessage(new InformationMessage("[ERROR]: Root or CultureMultipliers element missing in config!", Colors.Red));
            }

            InformationManager.DisplayMessage(new InformationMessage($"{CultureMxes.ToString()}"));

            var mods = ModuleHelper.GetModules();
            foreach ( var mod in mods )
            {
                InformationManager.DisplayMessage(new InformationMessage($"{mod.Name}: {mod.IsSelected}"));
                if (!mod.IsSelected) { continue; }

                InformationManager.DisplayMessage(new InformationMessage($"Exists: {File.Exists(mod.FolderPath + "\\ModuleData\\WeThePeople.xml")}"));
                if (File.Exists(mod.FolderPath + "\\ModuleData\\WeThePeople.xml"))
                {
                    XDocument MCG = XDocument.Load(mod.FolderPath + "\\ModuleData\\WeThePeople.xml");
                    Republics.AddRange(MCG.Element("WeThePeopleConfig").Element("RepublicKingdoms").Elements("Republic").Select(x => x.Value.Trim()).ToList());
                    CultureMxes.AddRange(doc.Element("WeThePeopleConfig")
                                            .Element("CultureMultipliers")
                                            .Elements("Culture")
                                            .Select(x => new CultureMx
                                            {
                                            Name = x.Element("Name").Value.Trim(),
                                            Militia = float.Parse(x.Element("Militia").Value),
                                            Food = float.Parse(x.Element("Food").Value),
                                            Garrison = float.Parse(x.Element("Garrison").Value),
                                            Loyalty = float.Parse(x.Element("Loyalty").Value),
                                            Security = float.Parse(x.Element("Security").Value),
                                            Prosperity = float.Parse(x.Element("Prosperity").Value)
                                            })
                                            .ToList());

                    foreach (var rem in MCG.Element("WeThePeopleConfig").Element("Remove").Elements("RemoveRepublic").Select(x => x.Value.Trim()).ToList()) { if (Republics.Contains(rem)) { Republics.Remove(rem); } }
                    foreach (var rem in MCG.Element("WeThePeopleConfig").Element("Remove").Elements("RemoveCulture").Select(x => x.Value.Trim()).ToList()) { if (rem != "DEFAULT") { CultureMxes.Remove(CultureMxes.Find(c => c.Name == rem)); } }
                }
            }
        }

        public override void RegisterEvents()
        {
            LoadConfig();
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Weekly);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, Rebellion);
        }

        private void GrabClans()
        {
            InformationManager.DisplayMessage(new InformationMessage("\nThis Week's Polls:", Colors.Yellow));
            RatingsList.Clear();
            ClanRatings.Clear();

            foreach (var state in Kingdom.All)
            {
                if ((state.Name.ToString().EndsWith("Republic") || state.Name.ToString().StartsWith("Republic of")) && !Republics.Contains(state.Name.ToString())) { Republics.Add(state.Name.ToString()); }
            }

            foreach (var name in Republics)
            {
                Kingdom? State = GetKingdomByName(name);
                if (State == null) { continue; }
                InformationManager.DisplayMessage(new InformationMessage($"Polls in {name}:", Colors.Yellow));

                foreach (var Clan in State.Clans)
                {
                    if (Clan.IsEliminated || !Clan.Fiefs.Any() || Clan.IsClanTypeMercenary) { continue; }

                    float TotalApproval = 0;
                    int FiefCount = 0;

                    foreach (var settlement in Clan.Fiefs)
                    {
                        float approval = Approval(settlement.Settlement);
                        TotalApproval += approval;
                        FiefCount++;
                        RatingsList.Add(new FiefRating(settlement.OwnerClan.StringId, settlement.Name.ToString(), approval, State.StringId));

                        if (approval < 15f)
                        {
                            settlement.Loyalty = 0;
                            InformationManager.DisplayMessage(new InformationMessage($"{settlement.Name} in {name} may rebel due to low approval!", Colors.Red));
                        }
                    }

                    if (FiefCount > 0)
                    {
                        float AverageApproval = TotalApproval / FiefCount;

                        InformationManager.DisplayMessage(new InformationMessage($"Clan {Clan.Name} Approval: {AverageApproval:F2}%"));

                        ClanRatings.Add(new ClanRating(Clan.StringId, AverageApproval, name));
                    }
                }
            }
        }

        private float Approval(Settlement settlement)
        {
            bool Castle = false;
            Town town;

            if (settlement.IsTown) { town = settlement.Town; Castle = false; }
            else if (settlement.IsCastle) { town = settlement.Town; Castle = true; }
            else { return 0; }

            string TCulture = town.Culture.ToString();
            CultureMx MX = CultureMxes.Find(c => c.Name == TCulture);
            if (MX == null)
            {
                //MX = CultureMxes[0];
                InformationManager.DisplayMessage(new InformationMessage($"[ERROR]: No multipliers found for {TCulture}"));
                return 0;
            }

            float Mil_Max = Castle ? Militia_Max_Castle : Militia_Max_Town;
            float Gar_Max = Castle ? Garrison_Max_Castle : Garrison_Max_Town;
            float Pro_Max = Castle ? Prosperity_Max_Castle : Prosperity_Max_Town;

            float Militia = town.Militia;
            float Food = town.FoodStocks;
            float Garrison = town.GarrisonParty.MemberRoster.TotalManCount;
            float Loyalty = town.Loyalty;
            float Security = town.Security;
            float Prosperity = town.Prosperity;

            if (town.IsUnderSiege)
            {
                return MathF.Clamp(((Militia / Mil_Max) * (Siege_Militia_Mx + MxRandomOffset()) + (Food / Food_Max) * (Siege_Food_Mx + MxRandomOffset()) + (Garrison / Gar_Max) * (Siege_Garrison_Mx + MxRandomOffset())) * 100, 0f, 100f);
            }
            else
            {
                return MathF.Clamp((((Militia / Mil_Max) * (MX.Militia + MxRandomOffset())) + (Food / Food_Max) * (MX.Food + MxRandomOffset()) + (Garrison / Gar_Max) * (MX.Garrison + MxRandomOffset()) + (Loyalty / 100) * (MX.Loyalty + MxRandomOffset()) + (Security / 100) * (MX.Security + MxRandomOffset()) + (Prosperity / Pro_Max) * (MX.Prosperity + MxRandomOffset())) * 100, 0f, 100f);
            }
        }

        private void Election()
        {
            InformationManager.DisplayMessage(new InformationMessage("\nNATIONAL ELECTIONS:", Colors.Yellow));
            var Sorted = ClanRatings.OrderByDescending(c => c.AverageRating).ToList();

            foreach (var name in Republics)
            {
                Kingdom? State = GetKingdomByName(name);
                if (State == null)
                {
                    continue;
                }

                InformationManager.DisplayMessage(new InformationMessage($"Election in {name}:", Colors.Yellow));
                ClanRating RCR = ClanRatings.Find(r => r.ClanName == State.RulingClan.StringId);

                if (RCR == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"[ERROR]: Could not find clan rating for {State.RulingClan.Name}"));
                    continue;
                }

                float Reelect = MBRandom.RandomFloat * 100f;
                InformationManager.DisplayMessage(new InformationMessage("[DEBUG]: " + RCR.AverageRating));

                if (Reelect > RCR.AverageRating)
                {
                    InformationManager.DisplayMessage(new InformationMessage($"{name}'s people chose to elect a new leader.", Colors.Green));

                    float Elect = 50f + (MathF.Pow(MBRandom.RandomFloat, 2f) * 50f);
                    float Closest = float.MaxValue;
                    List<ClanRating> SameCloseness = new List<ClanRating>();

                    foreach (var clanrating in Sorted)
                    {
                        if (clanrating.ClanName == RCR.ClanName) { continue; }
                        if (clanrating.AverageRating <= 50f) { continue; }
                        if (clanrating.Nation != name) { continue; }

                        float Dist = MathF.Abs(clanrating.AverageRating - Elect);

                        if (Dist < Closest)
                        {
                            SameCloseness.Clear();
                            SameCloseness.Add(clanrating);
                            Closest = Dist;
                        }
                        else if (Dist == Closest)
                        {
                            SameCloseness.Add(clanrating);
                        }
                    }

                    if (SameCloseness.Count == 1)
                    {
                        State.RulingClan = State.Clans.Find(c => c.StringId == SameCloseness[0].ClanName);
                        InformationManager.DisplayMessage(new InformationMessage($"{State.RulingClan.Name} is the new ruling clan of {name}!", Colors.Green));
                        RebelRisk.Remove(name);
                    }
                    else if (SameCloseness.Count > 1)
                    {
                        var rand = MBRandom.RandomInt(0, SameCloseness.Count);
                        State.RulingClan = State.Clans.Find(c => c.StringId == SameCloseness[rand].ClanName);
                        InformationManager.DisplayMessage(new InformationMessage($"{State.RulingClan.Name} is the new ruling clan of {name}!", Colors.Green));
                        RebelRisk.Remove(name);
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"{name}'s people did not vote! Large risk for rebellion!", Colors.Red));
                        if (!RebelRisk.Contains(name)) { RebelRisk.Add(name); }
                    }
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage($"{name}'s people have chosen to reelect the incumbent.", Colors.Green));
                    RebelRisk.Remove(name);
                }
            }
        }

        private void FiefElection(bool CheckRisk)
        {
            var Sorted = ClanRatings.OrderByDescending(c => c.AverageRating).ToList();
            InformationManager.DisplayMessage(new InformationMessage("Regional Elections:", Colors.Yellow));

            foreach (var name in Republics)
            {
                InformationManager.DisplayMessage(new InformationMessage($"{name} Regional:", Colors.Yellow));
                Kingdom? state = GetKingdomByName(name);
                bool Election = false;
                if (state == null) { continue; }

                foreach (var fief in RatingsList)
                {
                    //Town town = state.Fiefs.Find(f => f.Name.ToString() == fief.FiefName);
                    Town town = state.Fiefs.Where(f => state.StringId == fief.Nation).First(f => f.Name.ToString() == fief.FiefName);
                    if (town == null)
                    {
                        InformationManager.DisplayMessage(new InformationMessage($"[ERROR]: Could not find {fief.FiefName} in {state.Name}.", Colors.Red));
                        continue;
                    }

                    var Owner = town.OwnerClan;
                    float Reelect = MBRandom.RandomFloat * 100f;

                    if (Reelect > fief.Rating)
                    {
                        float Elect = 50f + (MathF.Pow(MBRandom.RandomFloat, 1.1f) * 50f);
                        float Closest = float.MaxValue;
                        List<ClanRating> SameCloseness = new List<ClanRating>();

                        int PickFiefless = MBRandom.RandomInt(0, 100);

                        if (PickFiefless >= 10)
                        {
                            foreach (var clan in Sorted)
                            {
                                if (clan.Nation != name)
                                    if (clan.ClanName == Owner.StringId) { continue; }
                                if (clan.AverageRating <= 50f) { continue; }

                                float Dist = MathF.Abs(clan.AverageRating - Elect);

                                if (Dist < Closest)
                                {
                                    SameCloseness.Clear();
                                    SameCloseness.Add(clan);
                                    Closest = Dist;
                                }
                                else if (Dist == Closest)
                                {
                                    SameCloseness.Add(clan);
                                }
                            }

                            if (SameCloseness.Count == 1)
                            {
                                town.OwnerClan = state.Clans.Find(c => c.StringId == SameCloseness[0].ClanName);
                                InformationManager.DisplayMessage(new InformationMessage($"{town.Name} elected {town.OwnerClan.GetName()} to represent them!", Colors.Green));
                                Election = true;
                            }
                            else if (SameCloseness.Count > 1)
                            {
                                var rand = MBRandom.RandomInt(0, SameCloseness.Count);
                                town.OwnerClan = state.Clans.Find(c => c.StringId == SameCloseness[rand].ClanName);
                                InformationManager.DisplayMessage(new InformationMessage(town.Name + " elected " + town.OwnerClan.Name + " to represent them!", Colors.Green));
                                Election = true;
                            }
                            else
                            {
                                if (CheckRisk)
                                {
                                    InformationManager.DisplayMessage(new InformationMessage($"{name}'s people did not vote! Large risk for rebellion!", Colors.Red));
                                    if (!RebelRisk.Contains(name)) { RebelRisk.Add(name); }
                                }
                            }
                        }
                        else
                        {
                            List<Clan> fiefless = new List<Clan>();

                            foreach (var clan in state.Clans)
                            {
                                if (clan.Fiefs.Any())
                                {
                                    continue;
                                }
                                fiefless.Add(clan);
                            }

                            if (fiefless.Count > 0)
                            {
                                town.OwnerClan = fiefless[MBRandom.RandomInt(0, fiefless.Count)];
                                InformationManager.DisplayMessage(new InformationMessage(town.Name + " chose the fiefless " + town.OwnerClan.Name + " to represent them!", Colors.Green));
                                Election = true;
                            }
                        }
                    }
                }

                if (!Election)
                {
                    InformationManager.DisplayMessage(new InformationMessage("All incumbent leaders were re-elected.", Colors.Red));
                }
                else
                {
                    if (CheckRisk)
                    {
                        RebelRisk.Remove(name);
                    }
                }
            }
        }

        private void Weekly()
        {
            InformationManager.DisplayMessage(new InformationMessage("[DEBUG] Weeks: " + _CycleWeek, Colors.Gray));
            GrabClans();

            if (_CycleWeek % Interval == 0)
            {
                FiefElection(false);
                Election();
            } else if (_CycleWeek == Interval / 2)
            {
                FiefElection(true);
            }

            _CycleWeek++;

            InformationManager.DisplayMessage(new InformationMessage("Test"));
        }

        private void Rebellion()
        {
            if (RebelRisk.Any())
            {

                foreach (var name in RebelRisk)
                {
                    Kingdom? state = GetKingdomByName(name);
                    if (state == null) { continue; }
                    int Rebel = 0;

                    foreach (var fief in state.Fiefs.Where(f => f != null))
                    {
                        int Rand = MBRandom.RandomInt(0, 100);
                        if (Rand < 10) { fief.Loyalty = 0; Rebel++; }
                    }

                    if (Rebel > 0)
                    {
                        InformationMessage RebelWarn = Rebel > 1 ? new InformationMessage($"Fiefs in {name} are planning to rebel due to political discontent!", Colors.Red) : new InformationMessage($"A fief in {name} is planing to rebel due to political discontent!", Colors.Red);
                        InformationManager.DisplayMessage(RebelWarn);

                    }
                }
            }
        }

        private void Hourly()
        {
            foreach (var c in CultureMxes)
            {
                InformationManager.DisplayMessage(new InformationMessage($"{c.Name}"));
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_CycleWeek", ref _CycleWeek);
        }
    }
}