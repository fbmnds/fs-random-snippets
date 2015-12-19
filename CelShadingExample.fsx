
// Antonio Cisternino 
// http://fssnip.net/H 

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
open System.IO
open System.Linq
open System.Reflection
open System.Xml
open System.Windows
open System.Windows.Media
open System.Windows.Markup
open System.Windows.Shapes
open System.Windows.Controls

open SharpGL
open SharpGL.SceneGraph.Core
open SharpGL.SceneGraph.Primitives
open SharpGL.SceneGraph
open SharpGL.VertexBuffers
open SharpGL.WPF
open GlmNet

module ManifestResourceLoader =
  
    /// <summary>
    /// Loads the named manifest resource as a text string.
    /// </summary>
    /// <param name="textFileName">Name of the text file.</param>
    /// <returns>The contents of the manifest resource.</returns>
    let LoadTextFile2(textFileName : string) =
    
        let executingAssembly = Assembly.GetExecutingAssembly()
        let pathToDots = textFileName.Replace("\\", ".")
        let location = String.Format("{0}.{1}", executingAssembly.GetName().Name, pathToDots)

        use stream = executingAssembly.GetManifestResourceStream(location)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    let LoadTextFile(textFileName : string) = 
        File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, textFileName))


//  Original Source: http://prideout.net/blog/?p=22

/// <summary>
/// The TrefoilKnot class creates geometry
/// for a trefoil knot.
/// </summary>
module TrefoilKnot = 
    open SharpGL.SceneGraph.Shaders
    open System.Collections.Generic
    open SharpGL.Shaders
    open SharpGL.Enumerations


    /// The number of slices and stacks.
    let slices = 128
    let stacks = 32
    let vertexCount = slices * stacks
    let indexCount = vertexCount * 6

    let mutable vertices : vec3[] = Array.init<vec3> vertexCount (fun _ -> new vec3()) 
    let mutable normals : vec3[] = Array.init<vec3> vertexCount (fun _ -> new vec3())
    let mutable indices : uint16[] = Array.init<uint16> indexCount (fun _ -> 0us)

    let vertexBuffer = new VertexBuffer()
    let normalBuffer = new VertexBuffer()

    let indexBuffer = new IndexBuffer()

    //  The vertex buffer array that handles the state of all vertex buffers.
    let vertexBufferArray = new VertexBufferArray()

    /// <summary>
    /// Evaluates the trefoil, providing the vertex at a given coordinate.
    /// </summary>
    /// <param name="s">The s.</param>
    /// <param name="t">The t.</param>
    /// <returns>The vertex at (s,t).</returns>
    let EvaluateTrefoil(s, t) : vec3 = 
     
        let TwoPi = (float32) Math.PI * 2.f
        let Cos (x : float32) = (float32) (Math.Cos (float x))
        let Sin (x : float32) = (float32) (Math.Sin (float x))

        let a = 0.5f
        let b = 0.3f
        let c = 0.5f
        let d = 0.1f
        let u = (1.0f - s) * 2.f * TwoPi
        let v = t * TwoPi
        let r = a + b * Cos (1.5f * u)
        let x = r * Cos u
        let y = r * Sin u
        let z = c * Sin (1.5f * u)

        let mutable dv = new vec3()
        dv.x <- -1.5f*b*(Sin (1.5f*u))*(Cos u) - (a + b*(Cos (1.5f*u))*(Sin u))
        dv.y <- -1.5f*b*(Sin (1.5f*u))*(Sin u) + (a + b*(Cos (1.5f*u))*(Cos u))
        dv.z <-  1.5f*c*(Cos (1.5f*u))
            
        let q = glm.normalize(dv)
        let qvn = glm.normalize(new vec3(q.y, -q.x, 0.0f))
        let ww =  glm.cross(q, qvn)

        let mutable range = new vec3()
        range.x <- x + d * (qvn.x*(Cos v) + ww.x*(Sin v))
        range.y <- y + d * (qvn.y*(Cos v) + ww.y*(Sin v))
        range.z <- z + d * ww.z * (Sin v)
            
        range

    let CreateVertexNormalBuffer(gl, vertexAttributeLocation, normalAttributeLocation) =

        let mutable count = 0

        let ds = 1.0f / (float32 slices)
        let dt = 1.0f / (float32 stacks)

        // The upper bounds in these loops are tweaked to reduce the
        // chance of precision error causing an incorrect # of iterations.

        let E = 0.01f
        for s in [ 0.f .. ds .. 1.f - ds / 2.f ] do
            for t in [ 0.f .. dt .. 1.f - dt / 2.f ] do
                let p = EvaluateTrefoil(s, t)
                let u = EvaluateTrefoil(s + E, t) - p
                let v = EvaluateTrefoil(s, t + E) - p
                let n = glm.normalize(glm.cross(u, v))
                vertices.[count] <- p
                normals.[count] <- n
                count <- count + 1

        //  Create the vertex data buffer.
        //let vertexBuffer = new VertexBuffer()
        vertexBuffer.Create(gl)
        vertexBuffer.Bind(gl)
        vertices |> Array.map (fun v -> v.to_array())
        |> Array.iter (fun x -> vertexBuffer.SetData(gl, vertexAttributeLocation, x, false, 3))
        vertexBuffer.Unbind(gl)
         
        //let normalBuffer = new VertexBuffer()
        normalBuffer.Create(gl)
        normalBuffer.Bind(gl)
        normals |> Array.map (fun v -> v.to_array())
        |> Array.iter (fun x -> normalBuffer.SetData(gl, normalAttributeLocation, x, false, 3))     
        normalBuffer.Unbind(gl)

    let CreateIndexBuffer(gl) =
        
        let mutable count = 0

        let mutable n = 0
        for  i in [0 .. slices-1] do
            for j in [0 .. stacks-1] do
                indices.[count] <- (uint16 (n + j))
                indices.[count+1] <- (uint16 (n + (j + 1) % stacks))
                indices.[count+2] <- (uint16 ((n + j + stacks) % vertexCount))

                indices.[count+3] <- (uint16 ((n + j + stacks) % vertexCount))
                indices.[count+4] <- (uint16 ((n + (j + 1) % stacks) % vertexCount))
                indices.[count+5] <- (uint16 ((n + (j + 1) % stacks + stacks) % vertexCount))
                count <- count+6          
            n <- n + stacks 

        //let indexBuffer = new IndexBuffer()
        do
            indexBuffer.Create(gl)
            indexBuffer.Bind(gl)
            indexBuffer.SetData(gl, indices)        
            indexBuffer.Unbind(gl)

    let GenerateGeometry (gl, vertexAttributeLocation, normalAttributeLocation) =
        //  Create the vertex array object. This will hold the state of all of the
        //  vertex buffer operations that follow it's binding, meaning instead of setting the 
        //  vertex buffer data and binding it each time, we can just bind the array and call
        //  DrawElements - much easier.
        //vertexBufferArray <- new VertexBufferArray()
        do
            vertexBufferArray.Create(gl)
            vertexBufferArray.Bind(gl)

            //  Generate the vertices and normals.
            CreateVertexNormalBuffer(gl, vertexAttributeLocation, normalAttributeLocation)
            CreateIndexBuffer(gl)

            vertexBufferArray.Unbind(gl)
        

    type Scene() =
        //  The shaders we use.
        let mutable shaderPerPixel = ShaderProgram()
        let mutable shaderToon = ShaderProgram()
        
        //  The modelview, projection and normal matrices.
        let mutable modelviewMatrix = mat4.identity()
        let mutable projectionMatrix = mat4.identity()
        let mutable normalMatrix = mat3.identity()

        let positionAttribute = 0u
        let normalAttribute = 1u
        let indexAttribute = 2u
        let attributeLocations = new Dictionary<uint32, string>()
        do 
            attributeLocations.Add (positionAttribute, "Position")
            attributeLocations.Add (normalAttribute, "Normal")
            attributeLocations.Add (indexAttribute, "Index")

        //  Scene geometry - a trefoil knot.
        //let trefoilKnot = new TrefoilKnot()   
        /// <summary>
        /// Initialises the Scene.
        /// </summary>
        /// <param name="gl">The OpenGL instance.</param>
        member x.Initialise(gl : OpenGL) =
        
            //  We're going to specify the attribute locations for the position and normal, 
            //  so that we can force both shaders to explicitly have the same locations.


            //  Create the per pixel shader.
            do shaderPerPixel.Create(gl, 
                                     ManifestResourceLoader.LoadTextFile(@"CelShadingExample\Shaders\PerPixel.vert"), 
                                     ManifestResourceLoader.LoadTextFile(@"CelShadingExample\Shaders\PerPixel.frag"), 
                                     attributeLocations)
            
            // Create the toon shader.
            do shaderToon.Create(gl,
                                 ManifestResourceLoader.LoadTextFile(@"CelShadingExample\Shaders\Toon.vert"),
                                 ManifestResourceLoader.LoadTextFile(@"CelShadingExample\Shaders\Toon.frag"), 
                                 attributeLocations)
            
            //  Generate the geometry and it's buffers.
            do GenerateGeometry(gl, positionAttribute, normalAttribute)
        

        /// <summary>
        /// Creates the projection matrix for the given screen size.
        /// </summary>
        /// <param name="gl">The OpenGL instance.</param>
        /// <param name="screenWidth">Width of the screen.</param>
        /// <param name="screenHeight">Height of the screen.</param>
        member x.CreateProjectionMatrix(gl : OpenGL, screenWidth : float32, screenHeight : float32) =
        
            //  Create the projection matrix for our screen size.
            let S = 0.46f
            let H = S * screenHeight / screenWidth
            let projectionMatrix = glm.frustum(-S, S, -H, H, 1.f, 100.f)

            //  When we do immediate mode drawing, OpenGL needs to know what our projection matrix
            //  is, so set it now.
            do
                gl.MatrixMode(OpenGL.GL_PROJECTION)
                gl.LoadIdentity()
                gl.MultMatrix(projectionMatrix.to_array())
                gl.MatrixMode(OpenGL.GL_MODELVIEW)
        

        /// <summary>
        /// Creates the modelview and normal matrix. Also rotates the sceen by a specified amount.
        /// </summary>
        /// <param name="rotationAngle">The rotation angle, in radians.</param>
        member x.CreateModelviewAndNormalMatrix(rotationAngle : float32) =
        
            //  Create the modelview and normal matrix. We'll also rotate the scene
            //  by the provided rotation angle, which means things that draw it 
            //  can make the scene rotate easily.
            let rotation = glm.rotate(mat4.identity(), rotationAngle, new vec3(0.f, 1.f, 0.f))
            let translation = glm.translate(mat4.identity(), new vec3(0.f, 0.f, -4.f))
            do
                modelviewMatrix <- rotation * translation
                normalMatrix <- modelviewMatrix.to_mat3()
        

        /// <summary>
        /// Renders the scene in immediate mode.
        /// </summary>
        /// <param name="gl">The OpenGL instance.</param>
        member x.RenderImmediateMode(gl : OpenGL) =
        
            //  Setup the modelview matrix.
            gl.MatrixMode(OpenGL.GL_MODELVIEW)
            gl.LoadIdentity()
            gl.MultMatrix(modelviewMatrix.to_array())
            
            //  Push the polygon attributes and set line mode.
            gl.PushAttrib(OpenGL.GL_POLYGON_BIT)
            gl.PolygonMode(FaceMode.FrontAndBack, PolygonMode.Lines)

            //  Render the trefoil.
            //let vertices = trefoilKnot.Vertices;
            gl.Begin(BeginMode.Triangles)
            //[|0 .. vertexCount-1|]// indices
            indices
            |> Array.iter (fun y -> 
                let x = (min (int y) (vertexCount - 1))
                gl.Vertex(vertices.[x].x, vertices.[x].y, vertices.[x].z)
                gl.Normal(normals.[x].x, normals.[x].y, normals.[x].z))
            gl.End()

            //  Pop the attributes, restoring all polygon state.
            gl.PopAttrib()
        

        /// <summary>
        /// Renders the scene in retained mode.
        /// </summary>
        /// <param name="gl">The OpenGL instance.</param>
        /// <param name="useToonShader">if set to <c>true</c> use the toon shader, otherwise use a per-pixel shader.</param>
        member x.RenderRetainedMode(gl : OpenGL, useToonShader) =

            gl.MatrixMode(OpenGL.GL_MODELVIEW)
            gl.LoadIdentity()
            gl.MultMatrix(modelviewMatrix.to_array())
                                                                        
            //GenerateGeometry(gl, positionAttribute, normalAttribute)
            //x.Initialise(gl)
            gl.Begin(BeginMode.Triangles)
            //[|0 .. vertexCount-1|]// indices
            indices
            |> Array.iter (fun y -> 
                let x = (min (int y) (vertexCount - 1))
                gl.Vertex(vertices.[x].x, vertices.[x].y, vertices.[x].z)
                gl.Normal(normals.[x].x, normals.[x].y, normals.[x].z))

            //  Get a reference to the appropriate shader.
            let shader = if useToonShader then shaderToon else shaderPerPixel

            //indexBuffer.Bind(gl)
            //indexBuffer.SetData(gl, indices) 

            //  Use the shader program.
            shader.Bind(gl)

            //  Set the variables for the shader program.
            shader.SetUniform3(gl, "DiffuseMaterial", 0.f, 0.75f, 0.75f)
            shader.SetUniform3(gl, "AmbientMaterial", 0.04f, 0.04f, 0.04f)
            shader.SetUniform3(gl, "SpecularMaterial", 0.5f, 0.5f, 0.5f)
            shader.SetUniform1(gl, "Shininess", 50.f)

            //  Set the light position.
            shader.SetUniform3(gl, "LightPosition", 0.25f, 0.25f, 1.f)

            //  Set the matrices.
            shader.SetUniformMatrix4(gl, "Projection", projectionMatrix.to_array())
            shader.SetUniformMatrix4(gl, "Modelview", modelviewMatrix.to_array())
            shader.SetUniformMatrix3(gl, "NormalMatrix", normalMatrix.to_array())

            //  Bind the vertex buffer array.
            vertexBufferArray.Bind(gl)      
            
            //  Draw the elements.
            //shader.GetInfoLog(gl) |> printfn "shader.GetInfoLog %s"
            //gl.UseProgram(shader.ShaderProgramObject)
            gl.DrawElements(OpenGL.GL_TRIANGLES, indices.Length, OpenGL.GL_UNSIGNED_SHORT, System.IntPtr(int indices.[0]))
            //gl.DrawArrays(OpenGL.GL_TRIANGLES, (int indices.[0]), indices.Length)
            //  Unbind the shader.
            shader.Unbind(gl)
            vertexBufferArray.Unbind(gl)
            //indexBuffer.Unbind(gl)
            gl.End()

module CelShadingExample =
    let mutable theta = 0.f
    let scene = new TrefoilKnot.Scene()
    let axies = new Axies()
    let mutable comboRenderModeSelectedIndex = 0
    let mutable checkBoxUseToonShaderIsChecked = false
    
     
    let OpenGLControl_OpenGLDraw(sender:obj, args : OpenGLEventArgs) =
        //  Get the OpenGL instance.
        let gl = args.OpenGL

        //  Add a bit to theta (how much we're rotating the scene) and create the modelview
        //  and normal matrices.
  
        theta <- theta + 0.015f
        scene.CreateModelviewAndNormalMatrix(theta)

        //  Clear the color and depth buffer.
        gl.ClearColor(1.f, 1.f, 1.f, 0.f)
        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT ||| OpenGL.GL_DEPTH_BUFFER_BIT ||| OpenGL.GL_STENCIL_BUFFER_BIT)
        //gl.Color(0.f,0.6f,0.8f,0.f)
      
        //gl.BlendColor(1.f,1.f,1.f,0.f)
        gl.Enable(OpenGL.GL_DEPTH_TEST)
        gl.DepthFunc(OpenGL.GL_LESS)         
            
        //  Render the scene in either immediate or retained mode.
        axies.Render(gl, RenderMode.Design)
        match comboRenderModeSelectedIndex with
        | 0 -> scene.RenderRetainedMode(gl, checkBoxUseToonShaderIsChecked)
        | 1 -> gl.Color(0.f,0.6f,0.8f,0.f)
               scene.RenderImmediateMode(gl)
        | _ -> () 
        
    let OpenGLControl_OpenGLInitialized(sender : obj, args : OpenGLEventArgs) =
        
        let gl = args.OpenGL
            
        //  Initialise the scene.
        scene.Initialise(gl)
        

    let OpenGLControl_Resized(sender : obj, args : OpenGLEventArgs) =
        
        //  Get the OpenGL instance.
        let gl = args.OpenGL       
        let s = sender :?> SharpGL.WPF.OpenGLControl
        //  Create the projection matrix for the screen size.
        scene.CreateProjectionMatrix(gl, float32 s.ActualWidth, float32 s.ActualHeight)//actualWidth, actualHeight)
        
    let ComboBox_Selected(sender : obj, args) =
        comboRenderModeSelectedIndex <- (sender :?> ComboBox).SelectedIndex
        
    let CheckBox_Selected(sender : obj, args) =
        checkBoxUseToonShaderIsChecked <- (sender :?> CheckBox).IsChecked.Value  

let loadXamlWindow (filename:string) =
    let reader = XmlReader.Create(filename)
    XamlReader.Load(reader) :?> Window

let app = new Application()

// Load the window.xaml file
let w =
    @"fs-random-snippets\CelShadingExample\MainWindow.xaml"
    |> fun x -> Path.Combine(System.Environment.GetEnvironmentVariable("PROJECTS"), x) 
    |> fun x -> loadXamlWindow(x)

let comboBox = w.FindName("comboRenderMode") :?> ComboBox
comboBox.SelectionChanged.Add (fun x -> CelShadingExample.ComboBox_Selected(comboBox,x))

let checkBox = w.FindName("checkBoxUseToonShader") :?> CheckBox
checkBox.Checked.Add (fun x -> CelShadingExample.CheckBox_Selected(checkBox,x))

//<sharpGL:OpenGLControl x:Name="openGlCtrl"
//OpenGLDraw="OpenGLControl_OpenGLDraw" OpenGLInitialized="OpenGLControl_OpenGLInitialized" 
//RenderContextType="FBO" Resized="OpenGLControl_Resized" />

let mutable openGlCtrl =  w.FindName("openGlCtrl") :?> SharpGL.WPF.OpenGLControl
openGlCtrl.InitializeComponent()

let gl  = SharpGL.OpenGL()
//gl.Create(SharpGL.Version.OpenGLVersion.OpenGL3_1, 
//          SharpGL.RenderContextType.FBO, 
//          (int openGlCtrl.ActualWidth), (int openGlCtrl.ActualHeight), 32, 
//          System.IntPtr.Zero) |> printfn "%A"

printfn "#0"
//CelShadingExample.scene.Initialise(gl)
printfn "#1"
openGlCtrl.OpenGLDraw.Add (fun x -> CelShadingExample.OpenGLControl_OpenGLDraw(openGlCtrl,x) )
printfn "#2"
openGlCtrl.OpenGLInitialized.Add (fun x -> CelShadingExample.OpenGLControl_OpenGLInitialized(openGlCtrl,x) )
printfn "#3"
openGlCtrl.RenderContextType <- RenderContextType.FBO
printfn "#4"
openGlCtrl.Resized.Add (fun x -> CelShadingExample.OpenGLControl_Resized(openGlCtrl,x) )
printfn "#5"
openGlCtrl.Visibility <- Visibility.Visible
printfn "#6"


w.Show()
