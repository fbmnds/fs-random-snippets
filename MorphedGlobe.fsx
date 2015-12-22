
#r "PresentationCore.dll";;
#r "PresentationFramework.dll";;
#r "WindowsBase.dll";;
#r "System.Xaml.dll";;
#r "UIAutomationTypes";;


open System.IO
open System.Windows
open System.Windows.Controls
open System.Windows.Markup
open System.Windows.Media


let resources = Path.Combine([|__SOURCE_DIRECTORY__; "MorphedGlobe"|])
let xaml = Path.Combine([|resources; "MeshMorphAnimationExample.xaml"|])
let world = Path.Combine([|resources; "world.jpg"|])


do 
    // Load xaml dynamically        
    let control = XamlReader.Load((new StreamReader(xaml)).BaseStream) :?> UserControl

    // Find the controls...
    let meshBrush = control.FindResource("MeshBrush") :?> ImageBrush 
    meshBrush.ImageSource <- new Imaging.BitmapImage(new System.Uri(world, System.UriKind.Absolute))
    let image = control.FindName("world") :?> Image 
    image.Source <- meshBrush.ImageSource

    Window(Content=control, Title="Morphed Globe")
    |> (Application()).Run
    |> ignore ;;