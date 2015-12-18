﻿
// Antonio Cisternino 
// http://fssnip.net/H 

#r "PresentationCore.dll";;
#r "PresentationFramework.dll";;
#r "System.Xaml.dll";;
#r "WindowsBase.dll";;

open System.Xml
open System.Windows
open System.Windows.Media
open System.Windows.Markup
open System.Windows.Shapes
open System.Windows.Controls

let loadXamlWindow (filename:string) =
  let reader = XmlReader.Create(filename)
  XamlReader.Load(reader) :?> Window

let app = new Application()

// Load the window.xaml file
let w = loadXamlWindow("window.xaml")
w.Show()

// We assume that there is an ellipse named Circle
let e = w.FindName("Circle") :?> Ellipse

// Register an event handler
e.MouseLeftButtonUp.Add(fun _ ->
  e.Fill <- 
    if e.Fill = (Brushes.Yellow :> Brush) then Brushes.Red
    else Brushes.Yellow
)

