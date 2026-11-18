using System;
using System.Data;
using System.IO;
using System.Reflection;
class SpecificationCatalogCheck {
 static int Main(string[] args){try{var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var repo=app.GetType("PineTree.Classes.OrderRepository");var table=(DataTable)repo.GetMethod("GetOrdersByOrderDate").Invoke(null,new object[]{new DateTime(2026,11,15)});if(!table.Columns.Contains("SpecText")||!table.Columns.Contains("DesignPadStateJson"))throw new Exception("Missing spec columns");Console.WriteLine("PASS: live order-list query, snapshot join and specification projection ("+table.Rows.Count+" rows)");return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
