using System.Reflection;
using Tesseract;

var methods = typeof(ResultIterator).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
foreach (var method in methods)
{
    Console.WriteLine(method.Name);
}
