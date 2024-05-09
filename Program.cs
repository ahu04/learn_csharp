using System.Runtime.InteropServices;

// // See https://aka.ms/new-console-template for more information
var hmap = new Flatmap<string, string>();
hmap["a"] = "gfucking";
hmap["b"] = "c";
hmap["c"] = "gfucking";
hmap["d"] = "b";
hmap["e"] = "c";
hmap["f"] = "gfucking";
hmap["g"] = "b";
hmap["h"] = "c";
hmap["i"] = "gfucking";
hmap["j"] = "b";
hmap["k"] = "c";
hmap.Remove("a");
hmap.Remove("b");
hmap.Remove("c");
hmap.Remove("d");
hmap.Remove("e");
hmap.Remove("f");
hmap.Remove("g");
hmap.Remove("h");
Console.WriteLine("{0}", hmap.ToString());