public static class RecipeTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static List<ForgeRecipeSpec> Pool(){var p=new List<ForgeRecipeSpec>();foreach(var r in ForgeBalance.Rules){var v=r.Value.Split(',');int limit=ForgeBalance.MechanismLimit(r.Key);p.Add(new ForgeRecipeSpec{Id=r.Key,Type=r.Key,Group=v[0],Cost=int.Parse(v[1]),Minimum=int.Parse(v[2]),Cap=int.Parse(v[3]),Special=r.Key=="HPSteal"||r.Key=="MPSteal"||limit>0,Mechanism=limit>0,Specialized=ForgeBalance.IsSpecialized(r.Key),Storage=false});}p.Add(new ForgeRecipeSpec{Id="Bag",Type="Storage",Group="storage",Storage=true,Special=true,Minimum=3,Cap=1});return p;}
 static Dictionary<string,int> Snapshot(params string[] categories){var d=new Dictionary<string,int>();foreach(string c in categories)d[c]=10;return d;}
 public static string Run(){
  for(int rarity=0;rarity<5;rarity++)Check(ForgeRecipes.Budget(rarity)==new[]{20,32,48,88,120}[rarity],"agreed material budgets in half-points");
  Check(SharedStatCatalog.Equipment("LEAF_DROP").EquipmentCost==SharedStatCatalog.RewardCost("LeafDrop") && SharedStatCatalog.Equipment("PLANET_DAMAGE").EquipmentCost==SharedStatCatalog.RewardCost("PlanetDamage"),"scalar reward and equipment prices stay unified");
  Check(ForgeAffinity.Strength("PLANET",6)==1.75 && ForgeAffinity.Strength("PLANET",10)==4,"planet intentionally uses 2/4/6/8/10 weighting");
  var pool=Pool();var lookup=new Dictionary<string,ForgeRecipeSpec>();foreach(var s in pool)lookup.Add(s.Id,s);
  Check(lookup.ContainsKey("FrostbiteDamage") && lookup["FrostbiteDamage"].Minimum==1 && lookup["FrostbiteDamage"].Specialized,"frostbite is a blue-or-higher specialty");
  Check(!lookup.ContainsKey("FreezeThreshold"),"freeze stack reduction must never enter forging pool");
  Check(lookup["FrostbiteDamage"].Cost==SharedStatCatalog.Equipment("FROSTBITE_DAMAGE").EquipmentCost,"frostbite shared price");
  var cometLegend=ForgeRecipes.BuildMechanism(lookup["DashCount"],lookup,88,"COMET");
  var cometEternal=ForgeRecipes.BuildMechanism(lookup["DashCount"],lookup,120,"COMET");
  Check(cometLegend.Category=="COMET" && cometLegend.Parts[0].Value==1 && cometLegend.Parts[1].Id=="DashAttackDamage" && cometLegend.Parts[1].Value==1,"legend comet fixed mechanism price");
  Check(cometEternal.Parts[0].Value==1 && cometEternal.Parts[1].Value==8,"eternal comet +1 and damage8, equipment +3 never leaks into reward");
  Check(lookup["Luck"].Minimum==2 && lookup["Luck"].Cost==24 && lookup["FinalWeaponDamage"].Cost==8,"new prices and luck rarity");
  Check(lookup["EXPDrop"].Cost==4 && SharedStatCatalog.Equipment("EXP_DROP").EquipmentCost==SharedStatCatalog.RewardCost("EXPDrop"),"experience equipment uses the same reward price");
  foreach(string id in new[]{"DARK_CLOUD_DAMAGE","DARK_CLOUD_SPEED","DARK_CLOUD_RESTORE_DURING_BATTLE"}){
   var definition=SharedStatCatalog.Equipment(id);Check(lookup[definition.Type].Cost==definition.EquipmentCost,"shared equipment and reward valuation: "+id);
  }
  Check(lookup["DarkCloudDamage"].Cost==4 && lookup["DarkCloudSpeed"].Cost==2 && lookup["DarkCloudRestoreDuringBattle"].Cost==2 && lookup["MinDarkCloud"].Cost==32 && lookup["LeafDrop"].Cost==2 && lookup["PlanetDamage"].Cost==4,"cloud, planet and economy reward prices");
  foreach(var t in ForgeTemplates.All)for(int count=-1;count<=15;count++){
   int tier=Math.Min(5,Math.Max(0,count/2));double expected=tier==0?0:1+3*Math.Pow((tier-1)/4.0,2);
   Check(ForgeAffinity.Strength(t.Category,count)==expected,"uniform affinity thresholds: "+t.Category);
  }
  foreach(var t in ForgeTemplates.All)if(t.Category=="SAVVY"&&!t.Fallback){
   Check(t.Sub=="LeafDrop"&&t.MainPercent==75,"gold is a 25 percent budget secondary stat");
   for(int budget=20;budget<=156;budget++){var card=ForgeRecipes.Build(t,lookup,budget);if(card!=null)Check(card.Parts[1].Value*lookup["LeafDrop"].Cost<=budget/4+lookup["LeafDrop"].Cost,"gold allocation bounded with integer rounding");}
  }
  foreach(var t in ForgeTemplates.All)if(t.Title=="乌云·循环")for(int rarity=2;rarity<5;rarity++){
   int budget=ForgeRecipes.BoostedBudget(rarity,30);var card=ForgeRecipes.Build(t,lookup,budget);
   Check(card!=null && card.Parts[0].Value*lookup["DarkCloudSpeed"].Cost>=budget*80/100 && card.Parts[1].Value*lookup["DarkCloudRestoreDuringBattle"].Cost<=budget*15/100+2,"cloud recovery stays a minor budget allocation");
  }
  var all=Snapshot("STURDY","ELEMENTAL","WINDSONG","GUARDIAN","PRECISION","SHADOW","ACADEMY","LAKE","FLAMESWORD","FROST","DARKCLOUD","EMBER","GLACIER","MAGITECH","CURSE","COMPANION","SAVVY","PLANET","FORTUNE","COMET","WEAPON","PARTY");
  var contexts=new[]{new Dictionary<string,int>(),all,Snapshot("FLAMESWORD"),Snapshot("FLAMESWORD","EMBER"),Snapshot("CURSE","SAVVY","COMPANION")};
  var seen=new HashSet<string>();int sets=0;var positions=new int[3];
  for(int rarity=0;rarity<5;rarity++)foreach(var context in contexts)foreach(bool enabled in new[]{true,false})for(int seed=0;seed<500;seed++){
   bool storage=seed%2==0;var cards=ForgeRecipes.Generate(pool,rarity,storage,new Random(seed),context,enabled);sets++;
   Check(cards.Count==3,"three choices");var unique=new HashSet<string>();int generics=0,mechanisms=0;
   for(int index=0;index<cards.Count;index++){
    var card=cards[index];Check(!string.IsNullOrEmpty(card.Title),"named template");Check(unique.Add(card.Signature),"dedup by composition across themes");
    if(card.Category==""){generics++;positions[index]++;}else Check(context.ContainsKey(card.Category)&&ForgeAffinity.Strength(card.Category,context[card.Category])>0,"only active themes");
    int cost=0;Check(card.Parts.Length>=1&&card.Parts.Length<=2,"one or two rewards");
    foreach(var part in card.Parts){var s=lookup[part.Id];seen.Add(s.Type);Check(s.Minimum<=rarity&&part.Value>0&&part.Value<=s.Cap,"rarity, positivity and cap");
     Check(enabled||!s.Specialized,"off excludes specialties");if(card.Category=="") {Check(!s.Specialized,"generic isolated");Check(s.Type!="FireDamage"&&s.Type!="IceDamage"&&s.Type!="LightningDamage","no single-element stats in generic cards");}
     Check(!s.Storage,"storage removed from random pool");if(s.Special)Check(part.Value==1,"single mechanism increment");
     if(s.Special&&!s.Mechanism)Check(card.Parts.Length==1,"steal standalone");
     if(s.Mechanism){mechanisms++;Check(card.Parts[0].Id==part.Id,"mechanism first");}
     if(!s.Special||s.Mechanism)cost+=s.Cost*part.Value;
    }
    Check(cost<=ForgeRecipes.Budget(rarity),"card budget");
   }
   Check(generics>=1&&mechanisms<=1,"generic guarantee and mechanism offer bound");
   for(int a=0;a<cards.Count;a++)for(int b=0;b<cards.Count;b++)if(a!=b)Check(!ForgeRecipes.Dominates(cards[a],cards[b]),"no strictly dominated choice");
   if(context.Count==0||!enabled&&context.ContainsKey("CURSE")&&context.Count==3)Check(generics==3,"no valid theme falls back to generic");
   if(!enabled&&context.Count==2&&context.ContainsKey("FLAMESWORD"))Check(generics==2,"same fire fallback offered only once");
   if(enabled&&context.Count==1&&rarity>=1)Check(generics==1,"single multi-template theme gives two distinct recipes");
  }
  foreach(var s in pool)if(!s.Storage)Check(seen.Contains(s.Type),"all existing attributes reachable: "+s.Type);
  var cheaper=new ForgeRecipeCard{Parts=new[]{new ForgeRecipePart{Id="PhysicalDamage",Value=1}}};
  var better=new ForgeRecipeCard{Parts=new[]{new ForgeRecipePart{Id="PhysicalDamage",Value=1},new ForgeRecipePart{Id="BasicAttackDamage",Value=2}}};
  Check(ForgeRecipes.Dominates(better,cheaper)&&!ForgeRecipes.Dominates(cheaper,better),"white physical-only regression");
  var remainder=ForgeRecipes.Build(new ForgeTemplate("PLANET","test","PlanetDamage","TrueDamage"),lookup,88);
  Check(remainder.Parts[0].Value==15&&remainder.Parts[1].Value==1,"planet reserves points for the next fixed increment");
  // Check every integer budget, not just rarity milestones or maximum Heart bonus.
  foreach(var t in ForgeTemplates.All)foreach(int cap in new[]{1,3,200}){
   var specs=new Dictionary<string,ForgeRecipeSpec>();foreach(var s in pool)specs[s.Type]=new ForgeRecipeSpec{Id=s.Id,Type=s.Type,Cost=s.Cost,Cap=Math.Min(s.Cap,cap),Special=s.Special};
   ForgeRecipeCard previous=null;
   for(int budget=0;budget<=156;budget++){
    var card=ForgeRecipes.Build(t,specs,budget);
    if(previous!=null)Check(card!=null,"eligible template cannot disappear as budget rises");
    if(card==null)continue;int spent=0;
    for(int i=0;i<card.Parts.Length;i++){var part=card.Parts[i];Check(part.Value>0&&part.Value<=specs[part.Id].Cap,"increment respects cap");spent+=part.Value*specs[part.Id].Cost;if(previous!=null)Check(part.Value>=previous.Parts[i].Value,"every budget monotonic: "+t.Title);}
    Check(spent<=budget,"increment never overspends");previous=card;
   }
  }
  for(int rarity=0;rarity<5;rarity++)foreach(var t in ForgeTemplates.All){
   ForgeRecipeCard previous=null;
   for(int level=0;level<=15;level++){
    var card=ForgeRecipes.Build(t,lookup,ForgeRecipes.BoostedBudget(rarity,level*2));
    if(previous!=null){Check(card!=null,"Heart level eligibility");for(int i=0;i<card.Parts.Length;i++)Check(card.Parts[i].Value>=previous.Parts[i].Value,"Heart level monotonic: "+t.Title);}
    previous=card;
   }
  }
  for(int rarity=0;rarity<5;rarity++)for(int seed=0;seed<200;seed++){
   var boosted=ForgeRecipes.Generate(pool,rarity,false,new Random(seed),all,true,30);
   for(int a=0;a<boosted.Count;a++){int spent=0;foreach(var part in boosted[a].Parts)spent+=part.Value*lookup[part.Id].Cost;Check(spent<=ForgeRecipes.BoostedBudget(rarity,30),"boosted card cannot overspend");for(int b=0;b<boosted.Count;b++)if(a!=b)Check(!ForgeRecipes.Dominates(boosted[a],boosted[b]),"boosted no dominated choice");}
  }
  foreach(int n in positions)Check(n>1000,"generic shuffled into every position");
  foreach(var t in ForgeTemplates.All){ForgeRecipeCard previous=null;
   for(int rarity=0;rarity<5;rarity++){
    var specs=new Dictionary<string,ForgeRecipeSpec>();foreach(var s in pool)if(s.Minimum<=rarity)specs[s.Type]=s;
    var card=ForgeRecipes.Build(t,specs,ForgeRecipes.Budget(rarity));if(card==null)continue;
    if(previous!=null)for(int i=0;i<card.Parts.Length;i++)Check(card.Parts[i].Value>=previous.Parts[i].Value,"rarity monotonic: "+t.Title);
    previous=card;
    if(t.Title=="守护·生存")Check(card.Parts[0].Value==new[]{1,2,3,6,9}[rarity]&&card.Parts[1].Value==new[]{2,3,5,9,12}[rarity],"guardian fixed purchase sequence");
    if(t.Title=="太阳剑·伤害")Check(card.Parts[0].Value==new[]{1,1,2,4,6}[rarity]&&card.Parts[1].Value==new[]{2,3,5,9,12}[rarity],"sword fixed purchase sequence");
   }
  }
  // Theme probability must not depend on the number of templates it owns.
  var counts=new Dictionary<string,int>{{"FLAMESWORD",0},{"GUARDIAN",0},{"PRECISION",0}};
  for(int seed=0;seed<6000;seed++){
   var cards=ForgeRecipes.Generate(pool,2,false,new Random(seed),Snapshot("FLAMESWORD","GUARDIAN","PRECISION"),true);
   var themes=new HashSet<string>();foreach(var c in cards)if(c.Category!=""){Check(themes.Add(c.Category),"different themes preferred");counts[c.Category]++;}
   Check(themes.Count==2,"two active themes");
  }
  foreach(int count in counts.Values)Check(count>3700&&count<4300,"template count cannot multiply theme probability");
  var equalCounts=new Dictionary<string,int>{{"SAVVY",0},{"GUARDIAN",0},{"PRECISION",0}};
  var equalSnapshot=new Dictionary<string,int>{{"SAVVY",4},{"GUARDIAN",4},{"PRECISION",4}};
  for(int seed=0;seed<6000;seed++)foreach(var c in ForgeRecipes.Generate(pool,2,false,new Random(seed),equalSnapshot,true))if(c.Category!="")equalCounts[c.Category]++;
  foreach(int count in equalCounts.Values)Check(count>3700&&count<4300,"four-combo negotiation has no special maximum-tier advantage");
  int strong=0,weak=0;var mixed=new Dictionary<string,int>{{"FLAMESWORD",10},{"GUARDIAN",2},{"PRECISION",2}};
  for(int seed=0;seed<6000;seed++)foreach(var c in ForgeRecipes.Generate(pool,2,false,new Random(seed),mixed,true))if(c.Category=="FLAMESWORD")strong++;else if(c.Category=="GUARDIAN")weak++;
  Check(strong>weak*1.4,"high-tier theme matters without excluding low-tier themes");
  int[] bags=new int[5],steals=new int[5],extra=new int[5];
  for(int rarity=3;rarity<=4;rarity++)for(int seed=0;seed<6000;seed++)foreach(var c in ForgeRecipes.Generate(pool,rarity,true,new Random(seed),all,true))foreach(var part in c.Parts){var s=lookup[part.Id];if(s.Storage)bags[rarity]++;else if(s.Mechanism)extra[rarity]++;else if(s.Special)steals[rarity]++;}
  Check(bags[3]==0&&bags[4]==0,"no storage in random rewards");
  Check(steals[3]>290&&steals[3]<430&&steals[4]>450&&steals[4]<630,"6/9 percent HP steal offers; removed MP chance not transferred");
  Check(extra[3]>280&&extra[3]<440&&extra[4]>510&&extra[4]<690,"6/10 percent mechanism offers");
  bool invalid=false;try{ForgeRecipes.Generate(pool,5,true,new Random(),all,true);}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"invalid rarity rejected");
  invalid=false;try{ForgeRecipes.Generate(new List<ForgeRecipeSpec>(),0,true,new Random(),all,true);}catch(InvalidOperationException){invalid=true;}Check(invalid,"insufficient templates rejected");
  return "PASS: "+sets+" scenario sets plus 24000 probability sets; theme fairness, high-tier preference, coverage, exact values, monotonicity, dedup, toggle, limits and shuffled generic";
 }
}
