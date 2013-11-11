#r @"nuget://AForge/2.2.5/lib/AForge.dll"
#r @"nuget://AForge.Neuro/2.2.5/lib/AForge.Neuro.dll"
#r @"nuget://AForge.Math/2.2.5/lib/AForge.Math.dll"
#r @"nuget://AForge.Imaging/2.2.5/lib/AForge.Imaging.dll"
#r @"nuget://Accord/2.10.0.0/lib/Accord.dll"
#r @"nuget://Accord.Math/2.10.0.0/lib/Accord.Math.dll"
#r @"nuget://Accord.Statistics/2.10.0.0/lib/Accord.Statistics.dll"
#r @"nuget://Accord.MachineLearning/2.10.0.0/lib/Accord.MachineLearning.dll"
#r @"nuget://Accord.Imaging/2.10.0.0/lib/Accord.Imaging.dll"
#r @"nuget://Accord.Neuro/2.10.0.0/lib/Accord.Neuro.dll"
#r @"nuget://WriteableBitmapEx/1.0.8/lib/net40/WriteableBitmapEx.Wpf.dll"
   
#r "Tsunami.IDEDesktop.exe"
#r "System.Windows.Forms.DataVisualization.dll"
#r "FSharp.Compiler.Interactive.Settings.dll"
#r "System.Xml"
#r "System.Drawing"
#r "System.Windows.Forms"
#r "System.Xml.Linq"
#r "WindowsBase"
#r "PresentationFramework"
#r "PresentationCore"
#r "System.Xaml"
#r "System.Xml"
#r "System.Xml.Linq"
#r "UIAutomationTypes"

open Accord.Neuro
open Accord.Neuro.Learning
open Accord.Neuro.Networks
open Accord.Neuro.ActivationFunctions
open AForge.Neuro.Learning
open Accord.Statistics.Analysis
open System.Windows.Ink
open System.IO
open System
open System.Threading
open System.Windows.Threading
open System.Net
open System.Xml
open System.Xml.Linq
open System
open System.Windows
open System.Windows.Controls
open System.Windows.Media.Imaging 
open System.Windows.Media 
open Tsunami.IDE.FSharp.Charting

let wc = new WebClient()
let xs = wc.DownloadString(@"http://tsunami.io/data/optdigits-tra.txt").Split([|"\r\n"|], StringSplitOptions.RemoveEmptyEntries)

let (X,y) = 
        [| for i in [0..33..63789] -> ([|for j in i..i+31 do yield! (xs.[j].ToCharArray() |> Array.map (string >> float))|], int xs.[i + 32]) |] 
        |> Array.unzip

(* Initiate Network *)
let dbn = DeepBeliefNetwork(BernoulliFunction(), 1024, 50, 10);

GaussianWeights(dbn).Randomize()
dbn.UpdateVisibleWeights()

(* Hyperparameters *)
let learningRate    = 0.1
let weigthDecay     = 0.001
let momentum        = 0.9
let batchSize       = 100

(* Split training data from testing data *)
let inputs = X |> Seq.take 1000 |> Seq.toArray
let outputs = y |> Seq.take 1000 |> Seq.toArray |> Array.map (fun x -> Array.init 10 (fun i -> if i = x then 1. else 0.))

let testInputs = X |> Seq.skip 1000 |> Seq.toArray
let testActuals = y |> Seq.skip 1000 |> Seq.toArray

// LearnLayerUnsupervised
let learnLayerUnspervised(layer:int,epochs:int) =
    let teacher = new DeepBeliefNetworkLearning(
                    dbn,
                    Algorithm = RestrictedBoltzmannNetworkLearningConfigurationFunction( fun h v i -> new ContrastiveDivergenceLearning(h, v, LearningRate = learningRate, Momentum = 0.5, Decay = weigthDecay) :> IUnsupervisedLearning),
                    LayerIndex = layer
                    )
    let batchCount = max 1 (inputs.Length / batchSize)
        
    // Create mini-batches to speed learning
    let groups = Accord.Statistics.Tools.RandomGroups(inputs.Length,batchCount)
    let batches = Accord.Math.Matrix.Subgroups(inputs,groups)
        
    let layerData = teacher.GetLayerInput(batches)
    let cd = teacher.GetLayerAlgorithm(teacher.LayerIndex) :?> ContrastiveDivergenceLearning

    // Start running the learning procedure
    [| for i in 0..epochs -> 
        if i = 10 then cd.Momentum <- momentum
        teacher.RunEpoch(layerData) / float inputs.Length |]

// LearnLayerSupervised
let learnLayerSupervised(epochs:int) =
        let teacher = DeepNeuralNetworkLearning(
                            dbn, 
                            Algorithm = ActivationNetworkLearningConfigurationFunction((fun ann i -> new ParallelResilientBackpropagationLearning(ann) :> ISupervisedLearning)), 
                            LayerIndex = 1)
        
        let layerData = teacher.GetLayerInput(inputs)
        let errors = [| for i in 0..epochs -> teacher.RunEpoch(layerData,outputs)  |]
        dbn.UpdateVisibleWeights()
        errors

// LearnNetworkSupervised
let learnNetworkSupervised(epochs:int) =
    let teacher = AForge.Neuro.Learning.BackPropagationLearning(dbn, LearningRate = learningRate, Momentum = momentum)
    let errors = [| for i in 0..epochs -> teacher.RunEpoch(inputs,outputs)  |]
    dbn.UpdateVisibleWeights()
    errors

(* This may take a while *)
let errors =
    [|
        yield! learnLayerUnspervised(0,200) 
        yield! learnLayerSupervised(2000)
        yield! learnNetworkSupervised(200)
    |]

(* Display Errors *)
Chart.FastLine(errors |> Array.mapi (fun i x -> (i+1,x))).AndYAxis(Title = "Error", Log = true).AndXAxis(Title="Epochs")

let computeOutput(layer:int) =
    let length = dbn.Layers.[layer].Neurons.Length
    [|for i in 0..length-1 -> dbn.Reconstruct(Array.init length (fun j -> if i = j then 1.0 else 0.0 ), layer)|]

let display() =    
    let display(X:float[][], width:int, height:int, rows:int, columns:int) =    
        let bms = WriteableBitmap((rows*width),(columns*height),72. ,72. ,Media.PixelFormats.Pbgra32,null)
        bms.ForEach(fun x y _ -> 255uy - byte (255. * X.[(y/height) * rows + (x/width)].[(y%height) * width + (x%width)]) |> fun v -> Color.FromRgb(v,v,v))
        Image(Width = float (rows*width), Height = float (columns*height), Source = bms, HorizontalAlignment = HorizontalAlignment.Left)

    let sp = StackPanel(HorizontalAlignment = HorizontalAlignment.Left)    
    let add x = sp.Children.Add(x) |> ignore
    let label x = TextBlock(Text = x, FontSize = 32., Margin = Thickness(10.)) |> add

    label "Layer 2 Weights:"
    add <| display(computeOutput(1),32,32,10,1)

    label "Layer 1 Weights:"
    add <| display(computeOutput(0),32,32,10,5)
            
    label "Training Data:"
    add <| display(X,32,32,40,25)

    ScrollViewer(Content = sp,HorizontalScrollBarVisibility = ScrollBarVisibility.Visible)

Tsunami.IDE.SimpleUI.addControlToNewDocument("Network Weights", fun _ -> display() :> UIElement)

module Array =
    /// returns the index of the max item
    let maxi xs = xs |> Array.mapi (fun i x -> (i,x)) |> Array.maxBy snd |> fst

let predicted = testInputs |> Array.map (fun xs -> dbn.Compute(xs) |> Array.maxi)

let confustionMatrix = (new GeneralConfusionMatrix(10, testActuals, predicted))
printfn "Accuracy: %f" confustionMatrix.OverallAgreement

let interactiveControl() = 
    let inkCanvas = 
        InkCanvas( Width = 200., Height = 200., EditingMode = InkCanvasEditingMode.Ink, 
                        Background = Brushes.White, Opacity = 0.,
                        VerticalAlignment = VerticalAlignment.Top,
                        DefaultDrawingAttributes = DrawingAttributes(Color = Colors.Black, 
                                                                        Width = 30., Height = 30., 
                                                                        FitToCurve = true, 
                                                                        IgnorePressure = true, 
                                                                        StylusTip = StylusTip.Ellipse))

    let ic = Canvas( Width = 200., Height = 200., VerticalAlignment = VerticalAlignment.Top, Background = Brushes.White)

    let inkBmp = WriteableBitmap(32,32,72.,72.,Media.PixelFormats.Pbgra32,null)
    let inkImg = Image(Source = inkBmp, Width = 200., Height = 200.,VerticalAlignment = VerticalAlignment.Top)
    ic.Children.Add(inkImg) |> ignore
    ic.Children.Add(inkCanvas) |> ignore

    let outBmp = WriteableBitmap(32,32,72.,72.,Media.PixelFormats.Pbgra32,null)
    let outImg = Border(BorderBrush = Brushes.LightBlue, BorderThickness = Thickness(4.), 
                        Child = Image(Source = outBmp, Width = 200., Height = 200.,VerticalAlignment = VerticalAlignment.Top),
                         Margin = Thickness(10.), Height = 208., Width = 208.,VerticalAlignment = VerticalAlignment.Top
                        ) 

    let clear = Button(Content = "Clear", Margin = Thickness(10.))
    let predictedOutput = TextBlock()
    let loadRandom = Button(Content = "Load Random", Margin = Thickness(10.))
    let randomInput = TextBlock()
    let rnd = Random()

    let predOut = Array.init 10 (fun _ -> Shapes.Rectangle(Height=0., Width=20.,Fill=Brushes.Blue, VerticalAlignment = VerticalAlignment.Bottom))
    let predGrid = Primitives.UniformGrid(Columns = 10, Rows = 2, Width = 200., Height = 40.)
    predOut |> Array.iter (predGrid.Children.Add >> ignore)
    [|0..9|] |> Array.iter (fun i -> predGrid.Children.Add(TextBlock(HorizontalAlignment = HorizontalAlignment.Center, Text = string i)) |> ignore)

    let updateSparkline(xs:float[]) =
        let max = xs |> Array.maxi
        (xs,predOut) ||> Array.iter2 (fun x pred -> pred.Height <- 20. * x; pred.Fill <- Brushes.Blue)
        predOut.[max].Fill <- Brushes.Green

    loadRandom.Click.Add(fun _ -> 
            let i = rnd.Next(testInputs.Length)
            inkCanvas.Strokes.Clear()
            let is = testInputs.[i]
            let actual = testActuals.[i]
            randomInput.Text <- sprintf "Random Input: %i" actual
            inkBmp.ForEach(fun x y _ -> if is.[y * 32 + x] > 0.5 then Colors.Black else Colors.White)
            let os = dbn.Compute(is)
            updateSparkline(os)
            let rs = dbn.Reconstruct(os)
            let predicted = (os |> Array.maxi)
            outImg.BorderBrush <- if predicted = actual then Brushes.Green else Brushes.Red
            predictedOutput.Text <- sprintf "Predicted Output: %i " predicted
            outBmp.ForEach(fun x y _ -> (1. - rs.[y*32 + x]) * 255. |> byte |> fun v -> Color.FromRgb(v,v,v)) |> ignore
            )

    let recalc() = 
        inkCanvas.Opacity <- 1.
        outImg.BorderBrush <- Brushes.LightBlue
        ic.Measure(Size(200.,200.))
        ic.Arrange(Rect(Size(200.,200.)))
        let rtb = new RenderTargetBitmap(200, 200, 96., 96., PixelFormats.Default);
        rtb.Render(ic)
        ic.Arrange(Rect(Point(4.,4.),Size(200.,200.)))
        let wb = WriteableBitmap(rtb)    
        let input =
            [| 
                for y in [|0..6..186|] do
                    for x in [|0..6..186|] ->
                            if wb.GetPixel(x + 3,y + 3).R > 0uy then 0. else 1.
                
            |] 
        inkBmp.ForEach(fun x y _ -> (1. - input.[y*32 + x]) * 255. |> byte |> fun v -> Color.FromRgb(v,v,v)) |> ignore
        randomInput.Text <- ""
        let os = dbn.Compute(input)
        updateSparkline(os)
        let rs = dbn.Reconstruct(os)
        predictedOutput.Text <-  sprintf "Predicted Output: %i "  (os |> Array.maxi)
        outBmp.ForEach(fun x y _ -> (1. - rs.[y*32 + x]) * 255. |> byte |> fun v -> Color.FromRgb(v,v,v)) |> ignore
        inkCanvas.Opacity <- 0.

    ic.PreviewMouseDown.Add(fun _ -> inkCanvas.Opacity <- 1.)
    ic.MouseUp.Add(fun _ -> recalc())
    inkCanvas.StrokeCollected.Add(fun _ -> recalc())
    clear.Click.Add(fun _ -> inkBmp.Clear(); inkCanvas.Strokes.Clear(); recalc())

    let mainGrid = Grid(Width = 500., Height = 350.)
    mainGrid.ColumnDefinitions.Add(ColumnDefinition())
    mainGrid.ColumnDefinitions.Add(ColumnDefinition())
    mainGrid.RowDefinitions.Add(RowDefinition(Height = GridLength(230.)))
    mainGrid.RowDefinitions.Add(RowDefinition(Height = GridLength(40.)))
    mainGrid.RowDefinitions.Add(RowDefinition(Height = GridLength(40.)))

    let sp = StackPanel()
    [randomInput; predictedOutput] |> Seq.iter (sp.Children.Add >> ignore)
    let sp2 = StackPanel(Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center)
    [|clear; loadRandom |] |> Seq.iter (sp2.Children.Add >> ignore)
    Grid.SetRow(sp,2)
    Grid.SetColumn(sp,1)
    Grid.SetColumn(outImg,1)
    Grid.SetRow(sp2,1)
    Grid.SetRow(predGrid,1)
    Grid.SetColumn(predGrid,1)
    [
        Border(BorderBrush = Brushes.LightBlue, BorderThickness = Thickness(4.), Margin = Thickness(10.), Height = 208., Width = 208., Child = ic,VerticalAlignment = VerticalAlignment.Top) :> UIElement; 
        upcast predGrid; upcast outImg; upcast sp; upcast sp2;
    ] |> Seq.iter (mainGrid.Children.Add >> ignore)
    
    mainGrid
    
Tsunami.IDE.SimpleUI.addControlToNewDocument("Interactive", fun _ -> interactiveControl() :> UIElement)
