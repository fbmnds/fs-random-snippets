(**
# Scripting OpenGL 3.1 2D with F# using WPF and SharpGL (Part 2)

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


#r @"SimpleRepl\Rx-Core.2.2.5\lib\net40\System.Reactive.Core.dll"
#r @"SimpleRepl\Rx-Interfaces.2.2.5\lib\net40\System.Reactive.Interfaces.dll"
#r @"SimpleRepl\Rx-Linq.2.2.5\lib\net40\System.Reactive.Linq.dll"
#r @"SimpleRepl\Rx-PlatformServices.2.2.5\lib\net40\System.Reactive.PlatformServices.dll"
#r @"SimpleRepl\Rx-XAML.2.2.5\lib\net40\System.Reactive.Windows.Threading.dll"
#r @"SimpleRepl\Simple.Wpf.FSharp.Repl.1.23.0.0\lib\net40\Simple.Wpf.FSharp.Repl.Themes.dll"
#r @"SimpleRepl\Simple.Wpf.FSharp.Repl.1.23.0.0\lib\net40\Simple.Wpf.FSharp.Repl.dll"
#r @"SimpleRepl\Simple.Wpf.Terminal.1.34.0.0\lib\net40\Simple.Wpf.Terminal.Themes.dll"
#r @"SimpleRepl\Simple.Wpf.Terminal.1.34.0.0\lib\net40\Simple.Wpf.Terminal.dll"
#r @"SimpleRepl\System.Windows.Interactivity.WPF.2.0.20525\lib\net40\Microsoft.Expression.Interactions.dll"
#r @"SimpleRepl\System.Windows.Interactivity.WPF.2.0.20525\lib\net40\System.Windows.Interactivity.dll"
#r @"SimpleRepl\SharpZipLib.0.86.0\lib\20\ICSharpCode.SharpZipLib.dll"


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

open System.Reactive
open System.Reactive.Linq
open System.Reactive.PlatformServices
open Simple.Wpf.FSharp.Repl.Themes
open Simple.Wpf.FSharp.Repl
open Simple.Wpf.Terminal.Themes
open Simple.Wpf.Terminal
open Microsoft.Expression
open System.Windows.Interactivity
open ICSharpCode.SharpZipLib

let mutable glId = 0

type Base =

    /// Load the shader sourcecode from the named manifest resource.
    static member LoadManifestResource (textFileName : string) =
        let executingAssembly = Assembly.GetExecutingAssembly()
        let pathToDots = textFileName.Replace("\\", ".")
        let location = String.Format("{0}.{1}", executingAssembly.GetName().Name, pathToDots)

        use stream = executingAssembly.GetManifestResourceStream(location)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    /// Load the shader sourcecode from the text file.
    static member LoadTextFile (textFileName : string) = 
        if Regex(@"^(([a-zA-Z]:\\)|([a-zA-Z]://)|(//)).*").IsMatch(textFileName) then
            File.ReadAllText(textFileName)
        else
            File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, textFileName))

    /// Create an OpenGL instance of named version, otherwise throw an exception. 
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
        if gl.GetHashCode() <> glId then printfn "gl %d" (gl.GetHashCode())
        gl

    /// Activate the shader program with compiled and linked shaders.
    static member Init (gl : OpenGL) 
             (shaderProgram : ShaderProgram ref) 
             (attributeLocations : Dictionary<uint32, string>) 
             (pixelVertexShader : Shaders.VertexShader ref)
             (pixelFragmentShader : Shaders.FragmentShader ref)
             vertexShaderCode 
             fragmentShaderCode =
        if gl.GetHashCode() <> glId then printfn "Base.Init gl %d" (gl.GetHashCode())

        (!shaderProgram).Bind gl
        attributeLocations.AsEnumerable()
        |> Seq.iter (fun x -> (!shaderProgram).BindAttributeLocation(gl, x.Key, x.Value))
    
        let vertexShader gl shaderSource = 
            if gl.GetHashCode() <> glId then printfn "Base.Init vertexShader gl %d" (gl.GetHashCode())
            let shader =  Shaders.VertexShader()
            shader.CreateInContext gl
            shader.SetSource(shaderSource)
            shader.Compile()
            if not (shader.CompileStatus.HasValue) then 
                failwith (sprintf "failed with source:\n %s" shaderSource)
            if not (shader.CompileStatus.Value) then
                failwith (sprintf "failed with source:\n%s\ninfoLog:\n%s" shaderSource shader.InfoLog)
            shader

        let fragmentShader gl shaderSource = 
            if gl.GetHashCode() <> glId then printfn "Base.Init fragmentShader gl %d" (gl.GetHashCode())
            let shader =  Shaders.FragmentShader()
            shader.CreateInContext gl
            shader.SetSource(shaderSource)
            shader.Compile()
            if not (shader.CompileStatus.HasValue) then 
                failwith (sprintf "failed with source:\n %s" shaderSource)
            if not (shader.CompileStatus.Value) then
                failwith (sprintf "failed with source:\n%s\ninfoLog:\n%s" shaderSource shader.InfoLog)
            shader

        pixelVertexShader := vertexShader gl vertexShaderCode
        pixelFragmentShader := fragmentShader gl fragmentShaderCode

        (!pixelVertexShader).IsEnabled <- true
        (!pixelFragmentShader).IsEnabled <- true

        gl.AttachShader ((!shaderProgram).ShaderProgramObject, (!pixelVertexShader).ShaderObject)
        gl.AttachShader ((!shaderProgram).ShaderProgramObject, (!pixelFragmentShader).ShaderObject)
        gl.LinkProgram  ((!shaderProgram).ShaderProgramObject)
        gl.UseProgram   ((!shaderProgram).ShaderProgramObject)
        let buffer = [|for c in [0 .. 999] do yield " "|]
        gl.GetProgramInfoLog ((!shaderProgram).ShaderProgramObject, 1000, (buffer |> Base.IntPtr2), Text.StringBuilder())
        buffer |> Array.reduce (fun x y -> sprintf "%s%s" x y) |> printfn "GetProgramInfoLog %s"
        //gl.DetachShader ((!shaderProgram).ShaderProgramObject, (!pixelVertexShader).ShaderObject)
        //gl.DetachShader ((!shaderProgram).ShaderProgramObject, (!pixelFragmentShader).ShaderObject)

    /// Generic event handler for resizing the 2D SharpGL WPF control. 
    static member OnResized (sender : obj) (args : OpenGLEventArgs) =
        //  Get the OpenGL instance.
        let gl = args.OpenGL
        if gl.GetHashCode() <> glId then printfn "Base.Init OnResized gl %d" (gl.GetHashCode())
        //  Create an orthographic projection.
        gl.MatrixMode (Enumerations.MatrixMode.Projection)
        gl.LoadIdentity();
        gl.Ortho(0., (sender :?> OpenGLControl).ActualWidth, (sender :?> OpenGLControl).ActualHeight, 0., -10., 10.)

        //  Back to the modelview.
        gl.MatrixMode (Enumerations.MatrixMode.Modelview)

    /// Adapt a 2D function to the given size. 
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

    /// Load the XAML file and create the window.
    static member loadXamlWindow (filename:string) =
        let reader = XmlReader.Create(filename)
        XamlReader.Load(reader) :?> Window

    /// Retrieve the pointer to an array, i.e. to the address of the first array element.
    static member IntPtr x =
        let nativeint = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(x,0)
        System.IntPtr(nativeint.ToPointer())
    static member IntPtr2 x =
        let nativeint = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(x,0)
        System.IntPtr(nativeint.ToPointer())

module Globals = 

    let depth = 32

    let mainwindowxaml =
        """<Window 
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SharpGL Cube" Width="800" Height="600"
        xmlns:sharpGL="clr-namespace:SharpGL.WPF;assembly=SharpGL.WPF"
        xmlns:repl="clr-namespace:Simple.Wpf.FSharp.Repl.UI;assembly=Simple.Wpf.FSharp.Repl">

            <DockPanel Background="LightBlue">    
                <repl:ReplWindow x:Name="ReplWindow" DockPanel.Dock="Bottom" MaxHeight="300"/>   
                <sharpGL:OpenGLControl x:Name="openGlCtrl" DockPanel.Dock="Top" MinWidth="80" MinHeight="60"/>
            </DockPanel>

</Window>
"""

    let vertexShaderCode =
        """#version 140

#extension all : warn
         
// object space to camera space transformation
uniform mat4 modelview_matrix;             
            
// camera space to clip coordinates             
uniform mat4 projection_matrix; 

// incoming vertex position             
in vec3 vertex_position; 

// incoming vertex normal             
in vec3 vertex_normal; 
            
// transformed vertex normal             
out vec3 normal; 

void main(void)
{
    //not a proper transformation if modelview_matrix involves non-uniform scaling
    normal = ( modelview_matrix * vec4( vertex_normal, 0 ) ).xyz; 

    // transforming the incoming vertex position
    gl_Position = projection_matrix * modelview_matrix * vec4( vertex_position, 1 );
}
"""

    let fragmentShaderCode =
        """#version 140

#extension all : warn

precision highp float; 

const vec3 ambient = vec3( 0.1, 0.1, 0.1 );             
const vec3 lightVecNormalized = normalize( vec3( 0.5, 0.5, 2 ) );             
const vec3 lightColor = vec3( 0.5, 0.8, 0.2 ); 

in vec3 normal; 
out vec4 out_frag_color; 

void main(void)
{
    float diffuse = clamp( dot( lightVecNormalized, normalize( normal ) ), 0.0, 1.0 ); 
    out_frag_color = vec4( ambient + diffuse * lightColor, 1.0 ); 
}"""

    let mutable theta = 0.f
    let mutable delta = 5.f/360.f

//    let vertexShaderHandle = 0
//    let fragmentShaderHandle= 0
//    let shaderProgramHandle = 0
    let mutable modelviewMatrixLocation = 0
    let mutable projectionMatrixLocation = 0
    let mutable positionVboHandle = [|System.UInt32.MaxValue|]
    let mutable normalVboHandle = [|System.UInt32.MaxValue|]
    let mutable indicesVboHandle = [|System.UInt32.MaxValue|]
    
    let positionAttribute = 0u
    let normalAttribute = 1u
    let indexAttribute = 2u
     
    let projectionMatrix = ref (mat4()) 
    let modelviewMatrix = ref (mat4())
        
    let positionVboData2 = 
        [| -1.0f; -1.0f;  1.0f;
            1.0f; -1.0f;  1.0f;
            1.0f;  1.0f;  1.0f;
           -1.0f;  1.0f;  1.0f;   
           -1.0f; -1.0f; -1.0f;   
            1.0f; -1.0f; -1.0f; 
            1.0f;  1.0f; -1.0f;   
           -1.0f;  1.0f; -1.0f; |]  |> Array.map (fun x -> x*1.f)

    let positionVboData = 
        positionVboData2      
        |> Array.chunkBySize 3
        |> Array.map (fun x -> vec3 (x.[0],x.[1],x.[2]))

    let positionVboDataPtr = positionVboData |> Base.IntPtr

    let normalVboData = positionVboData

    let normalVboDataPtr = normalVboData |> Base.IntPtr
                          
    let indicesVboData = [| // front face                 
                            0us; 1us; 2us; 2us; 3us; 0us;                 
                            // top face                 
                            3us; 2us; 6us; 6us; 7us; 3us;                 
                            // back face                 
                            7us; 6us; 5us; 5us; 4us; 7us;                 
                            // left face                 
                            4us; 0us; 3us; 3us; 7us; 4us;                 
                            // bottom face                 
                            0us; 1us; 5us; 5us; 4us; 0us;                 
                            // right face                 
                            1us; 5us; 6us; 6us; 2us; 1us |]
                         //|> Array.map (int) 

    let indicesVboDataPtr = indicesVboData |> Base.IntPtr

    let shaderProgram = ref (ShaderProgram())

    let attributeLocations = new Dictionary<uint32, string>()        
    attributeLocations.Add (positionAttribute, "vertex_position")
    attributeLocations.Add (normalAttribute, "vertex_normal")

    let pixelVertexShader = ref (Shaders.VertexShader())
    let pixelFragmentShader = ref (Shaders.FragmentShader())

    let QueryMatrixLocations (gl : OpenGL) =    
        if gl.GetHashCode() <> glId then printfn "Globals QueryMatrixLocations gl %d" (gl.GetHashCode())
        projectionMatrixLocation <- gl.GetUniformLocation ((!shaderProgram).ShaderProgramObject, "projection_matrix")
        modelviewMatrixLocation <- gl.GetUniformLocation ((!shaderProgram).ShaderProgramObject, "modelview_matrix")
    
    let SetModelviewMatrix (gl : OpenGL) matrix =
        if gl.GetHashCode() <> glId then printfn "Globals SetModelviewMatrix gl %d" (gl.GetHashCode())
        modelviewMatrix := matrix
        gl.UniformMatrix4 (modelviewMatrixLocation, 1, false, (!modelviewMatrix).to_array())

    let SetProjectionMatrix (gl : OpenGL) matrix =
        if gl.GetHashCode() <> glId then printfn "Globals SetProjectionMatrix gl %d" (gl.GetHashCode())
        projectionMatrix := matrix
        gl.UniformMatrix4 (projectionMatrixLocation, 1, false, (!projectionMatrix).to_array()) 

    let LoadVertexPositions (gl : OpenGL) =
        if gl.GetHashCode() <> glId then printfn "Globals LoadVertexPositions gl %d" (gl.GetHashCode())
        gl.GenBuffers (1, positionVboHandle)
        gl.BindBuffer (OpenGL.GL_ARRAY_BUFFER, positionVboHandle.[0])
        gl.BufferData (OpenGL.GL_ARRAY_BUFFER, positionVboData.Length, positionVboDataPtr, OpenGL.GL_STATIC_DRAW)
        gl.EnableVertexAttribArray positionVboHandle.[0]
        gl.BindAttribLocation ((!shaderProgram).ShaderProgramObject, positionVboHandle.[0], "vertex_position")
        gl.VertexAttribPointer (positionAttribute, 3, OpenGL.GL_FLOAT, false, 0, positionVboDataPtr)

    let LoadVertexNormals (gl : OpenGL) =
        if gl.GetHashCode() <> glId then printfn "Globals LoadVertexNormals gl %d" (gl.GetHashCode())
        gl.GenBuffers (1, normalVboHandle)             
        gl.BindBuffer (OpenGL.GL_ARRAY_BUFFER, normalVboHandle.[0])      
        gl.BufferData (OpenGL.GL_ARRAY_BUFFER, normalVboData.Length, normalVboDataPtr, OpenGL.GL_STATIC_DRAW)
        gl.EnableVertexAttribArray normalVboHandle.[0]
        gl.BindAttribLocation ((!shaderProgram).ShaderProgramObject , normalVboHandle.[0], "vertex_normal")
        gl.VertexAttribPointer (normalAttribute, 3, OpenGL.GL_FLOAT, false, 0, normalVboDataPtr)

    let LoadIndexer (gl : OpenGL) =
        if gl.GetHashCode() <> glId then printfn "Globals LoadIndexer gl %d" (gl.GetHashCode())
        gl.GenBuffers (1, indicesVboHandle)
        gl.BindBuffer (OpenGL.GL_ELEMENT_ARRAY_BUFFER, indicesVboHandle.[0])
        gl.BufferData (OpenGL.GL_ELEMENT_ARRAY_BUFFER, indicesVboData.Length, indicesVboDataPtr, OpenGL.GL_STATIC_DRAW)    
        gl.EnableVertexAttribArray indicesVboHandle.[0]


module Main =

    // Wiring up the OpenGL 3D projection
    let Init3D (gl : OpenGL) (width : float) (height : float) = 
        if gl.GetHashCode() <> glId then printfn "Main Init3D gl %d" (gl.GetHashCode())
        printfn "Main Init3D width %f,  height %f" width height
        // Activate the shader program
        Base.Init 
            gl 
            Globals.shaderProgram 
            Globals.attributeLocations
            Globals.pixelVertexShader
            Globals.pixelFragmentShader
            Globals.vertexShaderCode
            Globals.fragmentShaderCode
        // Projection
        Globals.QueryMatrixLocations gl
        let widthToHeight = (float32 width) / (float32 height)  
        printfn "Main Init3D widthToHeight %f" widthToHeight
        Globals.SetProjectionMatrix gl (glm.perspective (1.3f, widthToHeight, 0.1f, 20.f))
        glm.rotate (mat4.identity (), Globals.theta, vec3 (0.3f,0.3f,0.3f))
        |> fun x -> x * glm.translate (mat4 (1.f), vec3 (0.f,0.f,-4.f))
        |> fun x -> Globals.SetModelviewMatrix gl x 
        // Wiring the shaders with the buffers
        Globals.LoadVertexPositions gl
        Globals.LoadVertexNormals gl
        Globals.LoadIndexer gl
        // Other state             
        gl.Enable (OpenGL.GL_DEPTH_TEST)
        //gl.Enable (OpenGL.GL_CULL_FACE)
    
    // Resize event function
    let Resize (gl : OpenGL) (size : Size) =
        if gl.GetHashCode() <> glId then printfn "Main Resize gl %d" (gl.GetHashCode())
        printfn "Main Resize width %f,  height %f" size.Width size.Height
        // Create an 3D projection.
        gl.MatrixMode (Enumerations.MatrixMode.Projection)
        gl.LoadIdentity()
        let widthToHeight = (float32 size.Width) / (float32 size.Height)    
        printfn "Main Resize widthToHeight %f" widthToHeight       
        Globals.SetProjectionMatrix gl (glm.perspective (1.3f, widthToHeight, 0.1f, 20.f))
        glm.rotate (mat4.identity (), Globals.theta, vec3 (0.3f,0.3f,0.3f))
        |> fun x -> x * glm.translate (mat4 (1.f), vec3 (0.f,0.f,-4.f))
        |> fun x -> Globals.SetModelviewMatrix gl x
        gl.Flush()

    // Drawing event function
    let Cube (gl : OpenGL) width height =
        if gl.GetHashCode() <> glId then printfn "Main Cube gl %d" (gl.GetHashCode())
        Globals.theta <- Globals.theta + Globals.delta
        glm.rotate (mat4.identity (), Globals.theta, vec3 (0.3f,0.3f,0.3f))
        |> fun x -> x * glm.translate (mat4 (1.f), vec3 (0.f,0.f,-4.f))
        |> fun x -> Globals.SetModelviewMatrix gl x
        gl.Viewport(0, 0, (int width), (int height))
        // Clear the control, set the background color
        gl.ClearColor (Base.White.[0], Base.White.[1], Base.White.[2], 0.8f)         
        gl.Clear (OpenGL.GL_COLOR_BUFFER_BIT ||| OpenGL.GL_DEPTH_BUFFER_BIT)
        //gl.DrawElements (OpenGL.GL_QUADS, 24, OpenGL.GL_INT, Globals.indicesVboDataPtr)
        gl.Color(Base.Blue)
        gl.DrawElements (OpenGL.GL_TRIANGLES, Globals.indicesVboData.Length, OpenGL.GL_UNSIGNED_SHORT, Globals.indicesVboDataPtr)
        // Enforce drawing
        gl.Flush ()

    // Schaffolding the main window
    let Run () =
        // Generate the main window from the XAML string
        let w = (Globals.mainwindowxaml |> XamlReader.Parse) :?> Window
        printfn "Main Run w width %f,  height %f" w.Width w.Height

        // Create the OpenGL control, enforce OpenGL 3.1 mode
        let openGlCtrl = w.FindName("openGlCtrl") :?> OpenGLControl
        openGlCtrl.InitializeComponent()
        openGlCtrl.OpenGL.Create(Version.OpenGLVersion.OpenGL3_1, RenderContextType.FBO, 
                                    (int w.Width), (int w.Height), Globals.depth, (box [||]))
        |> fun x -> if not x then failwith "openGlCtrl.OpenGL.Create failed"
        openGlCtrl.OpenGLVersion <- Version.OpenGLVersion.OpenGL3_1
    
        glId <- openGlCtrl.OpenGL.GetHashCode()
        
        // Enable the OpenGL projection for 3D
        Init3D openGlCtrl.OpenGL w.Width w.Height

        // Enable the onscreen frame rate info
        openGlCtrl.DrawFPS <- true

        // Activate the OpenGL drawing
        openGlCtrl.OpenGLDraw.AddHandler (fun s _ ->
            
            if (s :?> OpenGLControl).OpenGL.GetHashCode() <> glId then 
                printfn "Main openGlCtrl.OpenGLDraw.AddHandler (s :?> OpenGLControl) %d" ((s :?> OpenGLControl).OpenGL.GetHashCode()) 
            Cube (s :?> OpenGLControl).OpenGL (s :?> OpenGLControl).ActualWidth (s :?> OpenGLControl).ActualHeight)

        // Activate Resizing handler
        openGlCtrl.SizeChanged.AddHandler (fun s x -> 
            if (s :?> OpenGLControl).OpenGL.GetHashCode() <> glId then
                printfn "Main openGlCtrl.Resized.AddHandler (s :?> OpenGLControl) %d" ((s :?> OpenGLControl).OpenGL.GetHashCode()) 
            Resize (s :?> OpenGLControl).OpenGL x.NewSize)

        w.Title <- sprintf "%s - OpenGL %A" w.Title openGlCtrl.OpenGLVersion
    
        let repl = w.FindName("ReplWindow") :?> Simple.Wpf.FSharp.Repl.UI.ReplWindow
        repl.InitializeComponent()
        repl.SizeChanged.AddHandler (fun s x -> (s :?> Simple.Wpf.FSharp.Repl.UI.ReplWindow).RenderSize <- x.NewSize)
        repl.WorkingDirectory <- __SOURCE_DIRECTORY__ 
        repl.Margin <- Thickness(0.)
          
        w  

// Launch the application with the main window
Main.Run () |> (Application ()).Run
