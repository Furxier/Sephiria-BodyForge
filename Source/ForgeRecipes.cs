using System;
using System.Collections.Generic;
internal sealed class ForgeRecipeSpec {
 internal string Id,Group,Type="";internal int Minimum,Cost=2,Cap=100;
 internal bool Special,Storage,Specialized,Mechanism;
}
internal sealed class ForgeRecipePart {internal string Id;internal int Value;}
internal sealed class ForgeRecipeCard {
 internal string Title,Category;internal ForgeRecipePart[] Parts;
 internal string Signature {get {var ids=new List<string>();foreach(var p in Parts)ids.Add(p.Id);ids.Sort(StringComparer.Ordinal);return string.Join("|",ids.ToArray());}}
}
internal static class ForgeRecipes {
 internal static int Budget(int rarity) {if(rarity<0||rarity>4)throw new ArgumentOutOfRangeException("rarity");return new[]{30,44,64,88,120}[rarity];}
 internal static ForgeRecipeCard Build(ForgeTemplate t,Dictionary<string,ForgeRecipeSpec> specs,int budget) {
  ForgeRecipeSpec main,sub;
  if(!specs.TryGetValue(t.Main,out main)||main.Special||main.Cost<=0||main.Cap<=0)return null;
  int value=Math.Min(main.Cap,(t.Sub==null?budget:budget*3/5)/main.Cost);if(value<1)return null;
  var parts=new List<ForgeRecipePart>{new ForgeRecipePart{Id=main.Id,Value=value}};
  if(t.Sub!=null) {
   if(!specs.TryGetValue(t.Sub,out sub)||sub.Special||sub.Cost<=0||sub.Cap<=0||sub.Id==main.Id)return null;
   int extra=Math.Min(sub.Cap,(budget-value*main.Cost)/sub.Cost);if(extra<1)return null;
   parts.Add(new ForgeRecipePart{Id=sub.Id,Value=extra});
  }
  return new ForgeRecipeCard{Title=t.Title,Category=t.Category,Parts=parts.ToArray()};
 }
 internal static List<ForgeRecipeCard> Generate(List<ForgeRecipeSpec> pool,int rarity,bool storageAvailable,Random random,Dictionary<string,int> snapshot,bool specialized) {
  int budget=Budget(rarity);var specs=new Dictionary<string,ForgeRecipeSpec>();
  foreach(var s in pool)if(s.Minimum<=rarity&&s.Cap>0&&(!s.Specialized||specialized))specs[s.Type]=s;
  var templates=new Dictionary<string,List<ForgeRecipeCard>>(StringComparer.Ordinal);var fallback=new List<ForgeTemplate>();
  foreach(var t in ForgeTemplates.All) {
   if(t.OffOnly&&specialized||t.OnOnly&&!specialized)continue;
   if(t.Fallback){fallback.Add(t);continue;}Add(templates,Build(t,specs,budget));
  }
  foreach(var t in fallback)if(!templates.ContainsKey(t.Category))Add(templates,Build(t,specs,budget));
  List<ForgeRecipeCard> generic;
  if(!templates.TryGetValue("",out generic)||generic.Count<3)throw new InvalidOperationException("可用通用配方不足三种，未消耗材料");
  var weights=new Dictionary<string,double>(StringComparer.Ordinal);
  foreach(var key in templates.Keys){int count;if(key!=""&&snapshot.TryGetValue(key,out count)){double w=ForgeAffinity.Strength(key,count);if(w>0)weights[key]=w;}}
  ForgeRecipeCard special=null,mechanism=null;
  if(rarity>=3) {
   int roll=random.Next(100);
   if(roll<(rarity==3?6:9)) {
    var rare=pool.FindAll(s=>s.Special&&!s.Storage&&!s.Specialized&&s.Minimum<=rarity&&s.Cap>0);
    if(rare.Count>0){var s=rare[random.Next(rare.Count)];special=Special(s,s.Type=="HPSteal"?"通用·生命吸取":"通用·法力吸取","");}
   }
   // One probability check for the whole mechanism offer, not per template.
   if(specialized&&random.Next(100)<(rarity==3?6:10)) {
    var candidates=new Dictionary<string,List<ForgeRecipeCard>>();var categories=new List<string>();var chances=new List<double>();
    foreach(var s in pool) {
     string category=ForgeTemplates.MechanismCategory(s.Type);int count;
     if(!s.Mechanism||s.Minimum>rarity||s.Cap<=0||!snapshot.TryGetValue(category,out count))continue;
     double w=ForgeAffinity.Strength(category,count);if(w<=0)continue;
     var card=BuildMechanism(s,specs,budget);if(card==null)continue;
     List<ForgeRecipeCard> entries;if(!candidates.TryGetValue(category,out entries)){entries=new List<ForgeRecipeCard>();candidates.Add(category,entries);categories.Add(category);chances.Add(w);}entries.Add(card);
    }
    if(categories.Count>0){var choices=candidates[categories[PickIndex(chances,random)]];mechanism=choices[random.Next(choices.Count)];}
   }
  }
  var result=new List<ForgeRecipeCard>();var signatures=new HashSet<string>();var used=new HashSet<string>();
  for(int i=0;i<2;i++) {
   ForgeRecipeCard card=i==0?mechanism:null;
   if(card==null) {
    var categories=new List<string>();var chances=new List<double>();
    // Prefer different themes. Reuse only if no different eligible theme remains.
    for(int pass=0;pass<2&&categories.Count==0;pass++)foreach(var pair in weights) {
     if(pass==0&&used.Contains(pair.Key))continue;
     if(!templates[pair.Key].Exists(c=>!signatures.Contains(c.Signature)))continue;
     categories.Add(pair.Key);chances.Add(pair.Value);
    }
    if(categories.Count>0){string c=categories[PickIndex(chances,random)];var choices=templates[c].FindAll(x=>!signatures.Contains(x.Signature));card=choices[random.Next(choices.Count)];}
    else card=PickGeneric(generic,signatures,random);
   }
   result.Add(card);signatures.Add(card.Signature);if(card.Category!="")used.Add(card.Category);
  }
  result.Add(special??PickGeneric(generic,signatures,random));
  for(int i=result.Count-1;i>0;i--){int j=random.Next(i+1);var t=result[i];result[i]=result[j];result[j]=t;}return result;
 }
 internal static ForgeRecipeCard BuildMechanism(ForgeRecipeSpec main,Dictionary<string,ForgeRecipeSpec> specs,int budget) {
  if(!main.Mechanism||main.Cap<1||main.Cost<=0||main.Cost>budget)return null;
  var parts=new List<ForgeRecipePart>{new ForgeRecipePart{Id=main.Id,Value=1}};
  ForgeRecipeSpec sub;int left=budget-main.Cost;
  if(specs.TryGetValue(ForgeTemplates.MechanismSub(main.Type),out sub)&&!sub.Special&&sub.Cost>0&&sub.Cap>0){int value=Math.Min(sub.Cap,left/sub.Cost);if(value>0)parts.Add(new ForgeRecipePart{Id=sub.Id,Value=value});}
  return new ForgeRecipeCard{Title=ForgeTemplates.MechanismTitle(main.Type),Category=ForgeTemplates.MechanismCategory(main.Type),Parts=parts.ToArray()};
 }
 private static ForgeRecipeCard Special(ForgeRecipeSpec s,string title,string category){return new ForgeRecipeCard{Title=title,Category=category,Parts=new[]{new ForgeRecipePart{Id=s.Id,Value=1}}};}
 private static void Add(Dictionary<string,List<ForgeRecipeCard>> groups,ForgeRecipeCard card) {
  if(card==null)return;List<ForgeRecipeCard> list;
  if(!groups.TryGetValue(card.Category,out list)){list=new List<ForgeRecipeCard>();groups.Add(card.Category,list);}
  if(!list.Exists(c=>c.Signature==card.Signature))list.Add(card);
 }
 private static ForgeRecipeCard PickGeneric(List<ForgeRecipeCard> pool,HashSet<string> used,Random random) {
  var choices=pool.FindAll(c=>!used.Contains(c.Signature));if(choices.Count==0)throw new InvalidOperationException("没有可去重的通用配方，未消耗材料");return choices[random.Next(choices.Count)];
 }
 private static int PickIndex(List<double> weights,Random random) {
  double total=0;foreach(double w in weights)total+=w;double roll=random.NextDouble()*total;
  for(int i=0;i<weights.Count;i++){roll-=weights[i];if(roll<0)return i;}return weights.Count-1;
 }
}
