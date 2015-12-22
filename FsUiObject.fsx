module FSWpf

#r "WindowsBase"
#r "PresentationCore"
#r "PresentationFramework"
#r "System.Xaml"

open System
open System.Windows

[<AttributeUsage(AttributeTargets.Field, AllowMultiple = false)>]
type UiElementAttribute(name : string) = 
    inherit System.Attribute()
    new() = new UiElementAttribute(null)
    member this.Name = name

[<AbstractClass>]
type FsUiObject<'T when 'T :> FrameworkElement> (xamlPath) as this = 
    let loadXaml () = 
        use stream = System.IO.File.OpenRead(xamlPath)
        System.Windows.Markup.XamlReader.Load(stream)
    let uiObj = loadXaml() :?> 'T
    
    let flags = System.Reflection.BindingFlags.Instance ||| System.Reflection.BindingFlags.NonPublic ||| System.Reflection.BindingFlags.Public
    
    do  
        let fields = 
            this.GetType().GetFields(flags) 
            |> Seq.choose(fun f -> 
                let attrs =  f.GetCustomAttributes(typeof<UiElementAttribute>, false)
                if attrs.Length = 0 then None
                else
                    let attr = attrs.[0] :?> UiElementAttribute
                    Some(f, if String.IsNullOrEmpty(attr.Name) then f.Name else attr.Name)
                )
        for field, name in fields do
            let value = uiObj.FindName(name)
            if value <> null then
                field.SetValue(this, value)
            else
                failwithf "Ui element %s not found" name

    member x.UiObject = uiObj