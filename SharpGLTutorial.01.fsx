
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


/// F# convenience wrapper for SharpGL
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
    /// https://github.com/mattdesl/lwjgl-basics/wiki/GLSL-Versions
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

    /// Called after the window and OpenGL are initialized. Called exactly once, before the main loop.
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


module Globals = 
 
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

    let positionBufferObject = [|for i in [0u .. 8u] do yield i |]

    let vao = [|for i in [0u .. 2u] do yield 0u|]
    
    let shaderProgram = ShaderProgram()

    let attributeLocations = new Dictionary<uint32, string>()
    let positionAttribute = 0u
    let normalAttribute = 1u
    let indexAttribute = 2u        
    attributeLocations.Add (positionAttribute, "Position")
    //attributeLocations.Add (normalAttribute, "Normal")
    //attributeLocations.Add (indexAttribute, "outputColor")

    let mutable pixelVertexShader = Shaders.VertexShader()
    let mutable pixelFragmentShader = Shaders.FragmentShader()

module Main =

    let InitializeVertexBuffer (gl : OpenGL ref) =    
        (!gl).GenBuffers(1, Globals.positionBufferObject)

        (!gl).BindBuffer (OpenGL.GL_ARRAY_BUFFER, (Globals.positionBufferObject.[0]))
        (!gl).BufferData (OpenGL.GL_ARRAY_BUFFER, Globals.vertices, OpenGL.GL_STATIC_DRAW)
        (!gl).BindBuffer (OpenGL.GL_ARRAY_BUFFER, 0u)


    let Arrange (gl : OpenGL ref) =
        Base.Init 
            gl 
            Globals.shaderProgram
            Globals.attributeLocations
            Globals.vertexShaderCode
            Globals.fragmentShaderCode
        |> fun (x,y) -> 
            Globals.pixelVertexShader <- x
            Globals.pixelFragmentShader <- y
        InitializeVertexBuffer gl

        (!gl).GenVertexArrays (1, Globals.vao)
        (!gl).BindVertexArray(Globals.vao.[0])

    let Display (gl : OpenGL ref) =
        (!gl).ClearColor (0.f,0.6f,0.8f,1.f)  //(1.f,1.f,1.f,0.2f) //
        (!gl).Clear(OpenGL.GL_COLOR_BUFFER_BIT)
        if (fun _ -> DateTime.Now.Ticks % 10L)() = 0L then
            (!gl).Color (0.f,0.6f,0.8f,1.f)
        else 
            (!gl).Color (0.5f,0.6f,0.0f,1.f)
        

        (!gl).UseProgram(Globals.shaderProgram.ShaderProgramObject)

//        (!gl).BindBuffer(OpenGL.GL_ARRAY_BUFFER, Globals.positionBufferObject.[0])
//        (!gl).EnableVertexAttribArray(0u)
//        (!gl).VertexAttribPointer(0u, 4, OpenGL.GL_FLOAT, false, 0, IntPtr(int Globals.positionBufferObject.[0]))

        (!gl).DrawArrays(OpenGL.GL_TRIANGLES, 0, 3)
        (!gl).Flush()

//        (!gl).DisableVertexAttribArray (0u)
//        (!gl).UseProgram (0u)
//        Globals.shaderProgram.Unbind (!gl)
       
    let loadXamlWindow (filename:string) =
        let reader = XmlReader.Create(filename)
        XamlReader.Load(reader) :?> Window

    let ComboBox_Selected (sender : obj, x : EventArgs) = ()
    let CheckBox_Selected (sender : obj, x : EventArgs) = ()

    let Window () =
        // Load the window.xaml file
        let w =
            @"fs-random-snippets\CelShadingExample\MainWindow.xaml"
            |> fun x -> Path.Combine(System.Environment.GetEnvironmentVariable("PROJECTS"), x) 
            |> fun x -> loadXamlWindow(x)

        let comboBox = w.FindName("comboRenderMode") :?> ComboBox
        comboBox.SelectionChanged.Add (fun x -> ComboBox_Selected(comboBox,x))

        let checkBox = w.FindName("checkBoxUseToonShader") :?> CheckBox
        checkBox.Checked.Add (fun x -> CheckBox_Selected(checkBox,x))

        let mutable openGlCtrl =  w.FindName("openGlCtrl") :?> SharpGL.WPF.OpenGLControl
        
        openGlCtrl.InitializeComponent()
        openGlCtrl.OpenGL.Create(Version.OpenGLVersion.OpenGL3_1, RenderContextType.FBO, 800, 600, 32, (box [||]))
        |> printfn "openGlCtrl.OpenGL.Create %b"

        openGlCtrl.OpenGLVersion <- Version.OpenGLVersion.OpenGL3_1
        Arrange (ref openGlCtrl.OpenGL)

        openGlCtrl.DrawFPS <- true
        //openGlCtrl.Foreground <- Media.Brushes.White
        openGlCtrl.OpenGLDraw.Add (fun x -> Display (ref x.OpenGL))
//        openGlCtrl.Resized.Add (fun x -> Display (ref x.OpenGL))

//        [| 
//            fun _ -> () 
//            fun _ -> openGlCtrl.OpenGLDraw.Add (fun x -> Display (ref x.OpenGL)) 
//            fun _ -> openGlCtrl.OpenGLInitialized.Add (fun x -> Display (ref x.OpenGL))         
//            fun _ -> openGlCtrl.RenderContextType <- RenderContextType.FBO
//            fun _ -> openGlCtrl.Resized.Add (fun x -> Display (ref x.OpenGL))                    
//            fun _ -> openGlCtrl.Visibility <- Visibility.Visible            
//        |]
//        |> Array.iteri (fun i f -> f (); printfn "%d" i)

        w.Title <- sprintf "%s - OpenGL %A" w.Title openGlCtrl.OpenGLVersion
        w
 
Main.Window () |> (Application ()).Run |> ignore