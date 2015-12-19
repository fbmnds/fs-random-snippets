// see example at the bottom
module dgml

open System
open System.Collections.Generic
open System.Xml
open System.Xml.Serialization

type Graph() = 
    [<DefaultValue>] val mutable public Nodes : Node[]
    [<DefaultValue>] val mutable public Links : Link[]    

and Node() =
    [<XmlAttribute>] member val Id = "" with get, set
    [<XmlAttribute>] member val Label = "" with get, set

and Link()  =
    [<XmlAttribute>] member val Source = "" with get, set
    [<XmlAttribute>] member val Target = "" with get, set
    [<XmlAttribute>] member val Label  = "" with get, set

type DGMLWriter() =
    let Nodes = new List<Node>()
    let Links = new List<Link>()
    member m.AddNode id label = Nodes.Add(new Node(Id=id, Label=label))
    member m.AddLink src  trg  label = Links.Add(new Link(Source=src, Target=trg, Label=label))
    member m.Write (filename : string) =
        let g = Graph(Nodes=Nodes.ToArray(), Links=Links.ToArray())
        let root = new XmlRootAttribute("DirectedGraph")
        root.Namespace <- "http://schemas.microsoft.com/vs/2009/dgml"
        let serializer = new XmlSerializer(typeof<Graph>, root)
        let settings = new XmlWriterSettings(Indent=true)
        use xmlWriter = XmlWriter.Create(filename, settings)
        serializer.Serialize(xmlWriter, g);

// F# translation of http://stackoverflow.com/questions/8199600/c-sharp-directed-graph-generating-library

// create a graph and write it as dgml. Open the graph with Visual Studio for Visualization. 
let desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory)
let w = DGMLWriter()
w.AddNode "root" "GraphDocument"
w.AddNode "1.1" "Find"
w.AddNode "1.2" "Linking"
w.AddNode "1.3" "Load"
w.AddNode "1.4" "Groups"
w.AddNode "1.5" "View DGML"
w.AddNode "2.1" "Back/Forward"
w.AddNode "2.2" "Demos"
w.AddNode "2.3" "New DGML"
w.AddLink "root" "1.1" ""
w.AddLink "root" "1.2" ""
w.AddLink "root" "1.3" ""
w.AddLink "root" "1.4" ""
w.AddLink "root" "1.5" ""
w.AddLink "1.2" "root" ""
w.AddLink "1.1" "2.1" ""
w.AddLink "1.3" "2.2" ""
w.AddLink "1.4" "2.2" ""
w.AddLink "1.4" "2.3" ""
w.AddLink "1.5" "2.3" ""
w.Write (desktop + @"\a.dgml")

