using System;
using Microsoft.UI.Xaml;
using WinRT;

namespace SystemOptimizer;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ => new App());
    }
}
