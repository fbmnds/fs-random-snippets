
#r "PresentationCore.dll";;
#r "PresentationFramework.dll";;
#r "System.Xaml.dll";;
#r "WindowsBase.dll";;


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


/// Extending SharpGL.Shaders.ShaderProgram
module ShaderProgram =
    
    /// Loads the shader sourcecode from the named manifest resource.
    let LoadManifestResource (textFileName : string) =
    
        let executingAssembly = Assembly.GetExecutingAssembly()
        let pathToDots = textFileName.Replace("\\", ".")
        let location = String.Format("{0}.{1}", executingAssembly.GetName().Name, pathToDots)

        use stream = executingAssembly.GetManifestResourceStream(location)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    /// Loads the shader sourcecode from the text file.
    let LoadTextFile (textFileName : string) = 
        if Regex(@"^(([a-zA-Z]:\\)|([a-zA-Z]://)|(//)).*").IsMatch(textFileName) then
            File.ReadAllText(textFileName)
        else
            File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, textFileName))

    let CreateShader ((gl : OpenGL), shaderType, shaderCode) =
        let shader = gl.CreateShader (shaderType)
        printfn "GetShaderInfoLog 0"
        try
            gl.ShaderSource (shader, !ref(shaderCode))
        with | ex -> ex.Message |> printfn "GetShaderInfoLog 0.1 %s"
        try
            gl.CompileShader (shader)
        with | ex -> ex.Message |> printfn "GetShaderInfoLog 1 %s"

        let infoLog = Text.StringBuilder()
        let length = [|for i in [0..99] do yield "0"|]
        try 
            gl.GetShaderInfoLog (shader, 100, IntPtr(int length.[0]), infoLog)
        with | ex -> ex.Message |> printfn "GetShaderInfoLog 2 %s"
        length |> printfn "GetShaderInfoLog 3 %A" 

        shader



module Main = 
    let vertices =
        [| 0.75f;  0.75f; 0.0f; 1.0f;
           0.75f; -0.75f; 0.0f; 1.0f;
           -0.75f; -0.75f; 0.0f; 1.0f |]

    let positionBufferObject = [|0u|]

    let vao = [|0u|]
    
    let gl = OpenGL()
    gl.Create(SharpGL.Version.OpenGLVersion.OpenGL3_1, 
              SharpGL.RenderContextType.FBO, 
              800, 600, 32, 
              System.IntPtr.Zero) |> printfn "gl.Create %b"

    let vertexShaderCode =
        """
#version 310
layout(location = 0) in vec4 Position;
void main()
{
	gl_Position = Position;
}
"""

    let fragmentShaderCode =
        """
#version 310
out vec4 outputColor;
void main()
{
	outputColor = vec4(1.0f, 1.0f, 1.0f, 1.0f);
}
"""
    let shader = ShaderProgram.CreateShader(gl,OpenGL.GL_VERTEX_SHADER,vertexShaderCode)

    let attributeLocations = new Dictionary<uint32, string>()
    let positionAttribute = 0u
    let normalAttribute = 1u
    let indexAttribute = 2u        
    attributeLocations.Add (positionAttribute, "Position")
    attributeLocations.Add (normalAttribute, "Normal")
    //attributeLocations.Add (indexAttribute, "Index")

    let shaderProgram = ShaderProgram()
    shaderProgram.Create(gl, vertexShaderCode, fragmentShaderCode, attributeLocations)

    /// Called after the window and OpenGL are initialized. Called exactly once, before the main loop.
    let Init () =
        gl.GenBuffers(1, positionBufferObject)

        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, positionBufferObject.[0])
        gl.BufferData(OpenGL.GL_ARRAY_BUFFER, vertices, OpenGL.GL_STATIC_DRAW)
        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, 0u)

        gl.GenVertexArrays(1, vao)
        gl.BindVertexArray(vao.[0])

    let Display () =
        gl.ClearColor(0.0f, 0.0f, 0.0f, 0.0f)
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT)

        gl.UseProgram(shaderProgram.ShaderProgramObject)

        gl.BindBuffer(OpenGL.GL_ARRAY_BUFFER, positionBufferObject.[0])
        gl.EnableVertexAttribArray(0u)
        gl.VertexAttribPointer(0u, 4, OpenGL.GL_FLOAT, OpenGL.GL_FALSE>0u, 0, IntPtr(0))

        gl.DrawArrays(OpenGL.GL_TRIANGLES, 0, 3)

        gl.DisableVertexAttribArray(0u)
        gl.UseProgram(0u)
        gl.Flush()
       
    let loadXamlWindow (filename:string) =
        let reader = XmlReader.Create(filename)
        XamlReader.Load(reader) :?> Window

    let ComboBox_Selected (sender : obj, x : EventArgs) = ()
    let CheckBox_Selected (sender : obj, x : EventArgs) = ()

    let Run () =
        
        Init()

        let app = new Application()

        // Load the window.xaml file
        let w =
            @"fs-random-snippets\CelShadingExample\MainWindow.xaml"
            |> fun x -> Path.Combine(System.Environment.GetEnvironmentVariable("PROJECTS"), x) 
            |> fun x -> loadXamlWindow(x)

        let comboBox = w.FindName("comboRenderMode") :?> ComboBox
        comboBox.SelectionChanged.Add (fun x -> ComboBox_Selected(comboBox,x))

        let checkBox = w.FindName("checkBoxUseToonShader") :?> CheckBox
        checkBox.Checked.Add (fun x -> CheckBox_Selected(checkBox,x))

        //<sharpGL:OpenGLControl x:Name="openGlCtrl"
        //OpenGLDraw="OpenGLControl_OpenGLDraw" OpenGLInitialized="OpenGLControl_OpenGLInitialized" 
        //RenderContextType="FBO" Resized="OpenGLControl_Resized" />

        let mutable openGlCtrl =  w.FindName("openGlCtrl") :?> SharpGL.WPF.OpenGLControl
        openGlCtrl.InitializeComponent()
        ref (openGlCtrl.OpenGL) := gl

        printfn "#0"
        Init()
        printfn "#1"
        openGlCtrl.OpenGLDraw.Add (fun _ -> ())
        printfn "#2"
        openGlCtrl.OpenGLInitialized.Add (fun _ -> ())
        printfn "#3"
        openGlCtrl.RenderContextType <- RenderContextType.FBO
        printfn "#4"
        openGlCtrl.Resized.Add (fun _ -> ())
        printfn "#5"
        openGlCtrl.Visibility <- Visibility.Visible
        printfn "#6"

        w.Show()
        Display()

Main.Run()