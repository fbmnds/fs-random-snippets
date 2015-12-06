// http://fsharpnews.blogspot.de/2010/07/lorenz-attractor.html

#r "PresentationCore.dll"
#r "PresentationFramework.dll"
#r "WindowsBase.dll"
#r "System.Xaml.dll"

let trajectory f (x, y, z) dt n = 
    let s, b, p = 10.0, 8.0 / 3.0, 28.0
    let mutable x = x
    let mutable y = y
    let mutable z = z
    let a = Array.zeroCreate n
    for i = 0 to n - 1 do
        a.[i] <- f (x, y, z)
        x <- x + s * (y - x) * dt
        y <- y + (x * (p - z) - y) * dt
        z <- z + (x * y - b * z) * dt
    a

open System.Windows

let whisp() = 
    let rand = System.Random()
    let f x = x + pown (rand.NextDouble() - 0.5) 2
    let x, y, z = f 10.0, f 0.0, f 20.0
    
    let xys = 
        let f (x, y, z) = Point(20.0 + x, 25.0 + y - z + 50.0)
        trajectory f (x, y, z) 0.003 2000
    
    let line_to (xy : Point) = (Media.LineSegment(xy, true) :> Media.PathSegment)
    Media.PathGeometry [ Media.PathFigure(xys.[0], Seq.map line_to xys, false) ]

do let group = Media.GeometryGroup()
   for i = 1 to 100 do
       whisp()
       |> group.Children.Add
       |> ignore
   let brush = Media.SolidColorBrush Media.Colors.Red
   let path = Shapes.Path(Data = group, Stroke = brush, StrokeThickness = 0.005)
   let box = Controls.Viewbox(Child = path, Stretch = Media.Stretch.Uniform)
   let window = Window(Content = box, Title = "Lorenz attractor")
   (Application()).Run window |> ignore

