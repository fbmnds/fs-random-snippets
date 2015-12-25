(**
# Scripting OpenGL 3.1 2D with F# using WPF and SharpGL (Part 1)

## Objectives

### Integration with  WPF 

The purpose of this F# script is to provide the necessary plumbing for using OpenGL within WPF. The canonical 
approach for embedding OpenGL in the window management system is based on the platform-independant C/C++ library GLUT. 
GLUT seems to be QT based while I was heading for a WPF solution. Without further investigation on OpenTK and other 
alternatives, I picked up Dave Kerr´s [SharpGL](https://github.com/dwmkerr/sharpgl) mostly because the author appears
to be proficient on the subject. The usage of SharpGL turned out to be smooth.

### Hardware Support for OpenGL 3.1

Although quite a bit outdated compared to the current version of OpenGL 4.4, I had to resort to OpenGL 3.1 because of
limitations imposed by the laptops I have in dayly use (not bleeding edge but decent enterprisy Lenovos). 
Another aspect is that OpenGL 3.1 relates to the current OenGL ES flavor of mobile platforms, namely on iPhone OS and 
Android. 

### Shader Infrastructure for 3D (Part 2, deferred)

OpenGL 3.1 already incorporates the shader infrastructure that is the conceptional baseline for the
recent OpenGL versions. This aspect will be covered in the next iteration of this script.   

## Dependencies

I built the current [SharpGL](https://github.com/dwmkerr/sharpgl) version easily from scratch using the provided build 
script. Otherwise, standard WPF/.NET libraries are referenced.

*)
#r "PresentationCore.dll";;
#r "PresentationFramework.dll";;
#r "System.Xaml.dll";;
#r "WindowsBase.dll";;
#r "UIAutomationTypes";;


#r @"SharpGL.WPF.2.4.0.0\SharpGL.dll"
#r @"SharpGL.WPF.2.4.0.0\SharpGL.WPF.dll"
#r @"SharpGL.WPF.2.4.0.0\SharpGL.SceneGraph.dll"
#r @"SharpGL.WPF.2.4.0.0\GlmNet.dll"
#r @"SharpGL.WPF.2.4.0.0\FileFormatWavefront.dll"
#r @"SharpGL.WPF.2.4.0.0\Apex.dll"


open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Reflection
open System.Text.RegularExpressions
open System.Xml
open System.Windows
open System.Windows.Media
open System.Windows.Markup
open System.Windows.Shapes
open System.Windows.Controls

open SharpGL
open SharpGL.Shaders
open SharpGL.SceneGraph.Core
open SharpGL.SceneGraph.Primitives
open SharpGL.SceneGraph
open SharpGL.VertexBuffers
open SharpGL.WPF
open GlmNet


(** 
## F# convenience wrapper for SharpGL

The `Base` type keeps convenience functions for SharpGL and generic functions related to windows management. 
It will be further enhanced for managing shader programs.
 
*)
type Base =
    (*** hide ***)    
    // Load the shader sourcecode from the named manifest resource.
    static member LoadManifestResource (textFileName : string) =
        let executingAssembly = Assembly.GetExecutingAssembly()
        let pathToDots = textFileName.Replace("\\", ".")
        let location = String.Format("{0}.{1}", executingAssembly.GetName().Name, pathToDots)

        use stream = executingAssembly.GetManifestResourceStream(location)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
    (*** hide ***)
    // Load the shader sourcecode from the text file.
    static member LoadTextFile (textFileName : string) = 
        if Regex(@"^(([a-zA-Z]:\\)|([a-zA-Z]://)|(//)).*").IsMatch(textFileName) then
            File.ReadAllText(textFileName)
        else
            File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, textFileName))

    // Create an OpenGL instance of named version, otherwise throw an exception. 
    // https://github.com/mattdesl/lwjgl-basics/wiki/GLSL-Versions
    static member OpenGL (openGLVersion : Version.OpenGLVersion) 
                         (renderContextType : RenderContextType)
                         width height bitDepth 
                         (parameters : obj) =
        let gl = OpenGL()
        if not (gl.Create(openGLVersion, 
                          renderContextType, 
                          width, height, bitDepth, 
                          parameters)) then
            failwith (sprintf "Base.glCreate SharpGL.Version.OpenGLVersion %A" openGLVersion)       
        gl
    (*** hide ***)
    // Called after the window and OpenGL are initialized. Called exactly once, before the main loop.
    static member Init (gl : OpenGL ref) 
             (shaderProgram : ShaderProgram) 
             (attributeLocations : Dictionary<uint32, string>) 
             vertexShaderCode 
             fragmentShaderCode =
        shaderProgram.Bind(!gl)
        attributeLocations.AsEnumerable()
        |> Seq.iter (fun x -> shaderProgram.BindAttributeLocation(!gl, x.Key, x.Value))
    
        let vertexShader gl shaderSource = 
            let shader =  Shaders.VertexShader()
            shader.CreateInContext(!gl)
            shader.SetSource(shaderSource)
            shader.Compile()
            if not (shader.CompileStatus.HasValue) then 
                failwith (sprintf "failed with source:\n %s" shaderSource)
            if not (shader.CompileStatus.Value) then
                failwith (sprintf "failed with source:\n%s\ninfoLog:\n%s" shaderSource shader.InfoLog)
            shader

        let fragmentShader gl shaderSource = 
            let shader =  Shaders.FragmentShader()
            shader.CreateInContext(!gl)
            shader.SetSource(shaderSource)
            shader.Compile()
            if not (shader.CompileStatus.HasValue) then 
                failwith (sprintf "failed with source:\n %s" shaderSource)
            if not (shader.CompileStatus.Value) then
                failwith (sprintf "failed with source:\n%s\ninfoLog:\n%s" shaderSource shader.InfoLog)
            shader

        let pixelVertexShader = vertexShader gl vertexShaderCode
        let pixelFragmentShader = fragmentShader gl fragmentShaderCode

        pixelVertexShader.IsEnabled <- true
        pixelFragmentShader.IsEnabled <- true
    
        (!gl).AttachShader (shaderProgram.ShaderProgramObject, pixelVertexShader.ShaderObject)
        (!gl).AttachShader (shaderProgram.ShaderProgramObject, pixelFragmentShader.ShaderObject)
        (!gl).LinkProgram (shaderProgram.ShaderProgramObject)
        (!gl).DetachShader (shaderProgram.ShaderProgramObject, pixelVertexShader.ShaderObject)
        (!gl).DetachShader (shaderProgram.ShaderProgramObject, pixelFragmentShader.ShaderObject)

        pixelVertexShader, pixelFragmentShader

    // Generic event handler for resizing the SharpGL WPF control. 
    static member OnResized (sender : obj) (args : OpenGLEventArgs) =
        //  Get the OpenGL instance.
        let gl = args.OpenGL

        //  Create an orthographic projection.
        gl.MatrixMode (Enumerations.MatrixMode.Projection)
        gl.LoadIdentity();
        gl.Ortho(0., (sender :?> OpenGLControl).ActualWidth, (sender :?> OpenGLControl).ActualHeight, 0., -10., 10.)

        //  Back to the modelview.
        gl.MatrixMode (Enumerations.MatrixMode.Modelview)

    // Adapt the given 2D function to the given control size. 
    static member Fun2D x0 y0 width height xmin xmax dx (f : float -> float) = 
        let fx = [| for x in [xmin .. (xmax - xmin)*dx .. xmax] do yield x, f x; yield xmax,f xmax |]                
        let ymin, ymax = fx |> Array.map (fun (x,y) -> y) |> fun y -> (y |> Array.min), (y |> Array.max)
        fx
        |> Array.map (fun (x,y) -> 
            //a*xmin+b -> x0
            //a*xmax+b -> x0+width
            x0 + width*(x - xmin)/(xmax - xmin),
            //a*ymin+b -> y0+heigth
            //a*ymax+b -> y0
            y0 + (ymax - y)*height/(ymax - ymin))

    // Define RGB color codes.
    static member Black   = [| 0.0f; 0.0f; 0.0f |]
    static member Red     = [| 1.0f; 0.0f; 0.0f |]
    static member Green   = [| 0.0f; 1.0f; 0.0f |]
    static member Yellow  = [| 1.0f; 1.0f; 0.0f |]
    static member Blue    = [| 0.0f; 0.0f; 1.0f |]
    static member Magenta = [| 1.0f; 0.0f; 1.0f |]
    static member Cyan    = [| 0.0f; 1.0f; 1.0f |]
    static member White   = [| 1.0f; 1.0f; 1.0f |]

(*** hide ***)
module Globals = 

    let mutable width = 800
    let mutable height = 600
    let depth = 32

    let vertexShaderCode =
        """
        #version 140
        in vec4 Position;
        void main()
        {
        	gl_Position = Position;
        }
        """

    let fragmentShaderCode =
        """
        #version 140
        out vec4 fColor;
        void main()
        {
	        fColor = vec4(0.f,0.6f,0.8f,0.5f);
        }
        """

    let vertices =
        [| 0.75f;  0.75f; 0.0f; 1.0f;
           0.75f; -0.75f; 0.0f; 1.0f;
           -0.75f; -0.75f; 0.0f; 1.0f |]

    let colors = [| Base.Blue; Base.Green; Base.Yellow |] |> Array.collect id

    let positionBufferObject = [|for i in [0u .. 8u] do yield i |]

    let vao = [|for i in [0u .. 2u] do yield 0u|]
    
    let shaderProgram = ShaderProgram()

    let attributeLocations = new Dictionary<uint32, string>()
    let positionAttribute = 0u
    let normalAttribute = 1u
    let indexAttribute = 2u        
    attributeLocations.Add (positionAttribute, "Position")
    attributeLocations.Add (normalAttribute, "Normal")
    attributeLocations.Add (indexAttribute, "outputColor")

    let mutable pixelVertexShader = Shaders.VertexShader()
    let mutable pixelFragmentShader = Shaders.FragmentShader()

(**
##  Schaffolding the Main Window

The SharpGL library provides a WPF control for OpenGL conceptionally similar to a `Canvas` element.
I reuse here a `XAML` file that contains menu items that will remain unused for this basic script.
The mechanism would be straight forward to use for adapted functionality, e.g. for changing colors.
I keep the related cosde as placeholders for this reason.
*)
module Main =
    // Wiring up the OpenGL orthografic projection for viewing the x,y-plane in 2D from above
    let Init (gl : OpenGL) =
        // select clearing (background) color
        gl.ClearColor (0.0f, 0.0f, 0.0f, 0.0f)

        // initialize viewing values
        gl.MatrixMode (OpenGL.GL_PROJECTION)
        gl.LoadIdentity ()
        gl.Ortho (0.0, 1.0, 0.0, 1.0, -1.0, 1.0)
    // Using OpenGL 2D:
    let BlueRect (gl : OpenGL) =
        // Clear the control, set the background color.
        gl.ClearColor (0.8f,0.6f,0.f,0.f) 
        gl.Clear (OpenGL.GL_COLOR_BUFFER_BIT ||| OpenGL.GL_DEPTH_BUFFER_BIT)
        // Draw a blue rectangle
        gl.Color (Base.Blue)        
        gl.Begin (OpenGL.GL_POLYGON)
        gl.Vertex (25., 25., 0.0)
        gl.Vertex (75., 25., 0.0)
        gl.Vertex (75., 75., 0.0)
        gl.Vertex (25., 75., 0.0)
        gl.End ()
        // Draw a yellow line, this illustrates the x,y coordinates with origin in the upper left corner.
        gl.Color (Base.Yellow)                
        gl.Begin (OpenGL.GL_LINES)
        gl.Vertex (100., 100., 0.0)
        gl.Vertex (375., 175., 0.0)
        gl.End ()
        // Draw a function in the given area.
        gl.PointSize (1.5f)
        gl.Color (Base.Green)
        gl.Begin (OpenGL.GL_POINTS)
        Base.Fun2D 50. 250. 700. 200. (-2.*Math.PI) (Math.PI*3.5) 0.002 Math.Sin
        |>  Array.iter (fun (x,y) -> gl.Vertex (x, y, 0.0))
        gl.End ()
        // Enforce drawing.        
        gl.Flush ()
        gl.Finish ()
    // Load the XAML file and create the main window.
    let loadXamlWindow (filename:string) =
        let reader = XmlReader.Create(filename)
        XamlReader.Load(reader) :?> Window
    // Placeholder for wiring the window menue.
    let ComboBox_Selected (sender : obj, x : EventArgs) = ()
    let CheckBox_Selected (sender : obj, x : EventArgs) = ()
    // Initialize the main window:
    let Window () =
        // Load the window.xaml file.
        let w =
            @"fs-random-snippets\CelShadingExample\MainWindow.xaml"
            |> fun x -> Path.Combine(System.Environment.GetEnvironmentVariable("PROJECTS"), x) 
            |> fun x -> loadXamlWindow(x)
        // Placeholder for wiring the window menue.
        let comboBox = w.FindName("comboRenderMode") :?> ComboBox
        comboBox.SelectionChanged.Add (fun x -> ComboBox_Selected(comboBox,x))
        // Placeholder for wiring the window menue. 
        let checkBox = w.FindName("checkBoxUseToonShader") :?> CheckBox
        checkBox.Checked.Add (fun x -> CheckBox_Selected(checkBox,x))

        // Create the OpenGL control, enforce OpenGL 3.1 mode.
        let mutable openGlCtrl =  w.FindName("openGlCtrl") :?> SharpGL.WPF.OpenGLControl        
        openGlCtrl.InitializeComponent()
        openGlCtrl.OpenGL.Create(Version.OpenGLVersion.OpenGL3_1, RenderContextType.FBO, 
                                 Globals.height, Globals.width, Globals.depth, (box [||]))
        |> fun x -> if not x then failwith "openGlCtrl.OpenGL.Create failed"
        openGlCtrl.OpenGLVersion <- Version.OpenGLVersion.OpenGL3_1
        // Enable the onscreen frame rate info.
        openGlCtrl.DrawFPS <- true
        // Activate the orthografic 2D view mode.
        Init(openGlCtrl.OpenGL)
        // Activate the OpenGL drawing.
        openGlCtrl.OpenGLDraw.Add (fun x -> BlueRect (x.OpenGL))
        openGlCtrl.Resized.Add (fun x -> Base.OnResized openGlCtrl x) 
        // Return the window handle for registering in the main application loop.
        w.Title <- sprintf "%s - OpenGL %A" w.Title openGlCtrl.OpenGLVersion
        w
// Launch the application with the main window. 
Main.Window () |> (Application ()).Run |> ignore

(**
## Summary 

It is pleasantly easy to launch a OpenGL control within WPF. The basic 2D mode does not yet make use of the 
enhanced OpenGL features of the shader pipeline. I will sum up this in another post.
*)