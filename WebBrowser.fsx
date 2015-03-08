open System
open System.Drawing
open System.Windows.Forms

[<STAThread>]
do Application.EnableVisualStyles()

let form = new Form(Text = "Web Browser", 
                    Size = new Size(800,600))

let toolbar = new ToolStrip(Dock = DockStyle.Top)
let address = new ToolStripTextBox(Size = new Size(400,25))
let go = new ToolStripButton(DisplayStyle = ToolStripItemDisplayStyle.Text, 
                             Text = "Go")

let browser = new WebBrowser(Dock = DockStyle.Fill)

let statusProgress = 
    new ToolStripProgressBar (Size = new Size(200,16),
                              Style = ProgressBarStyle.Marquee,
                              Visible = false)
let status = new StatusStrip(Dock = DockStyle.Bottom)
status.Items.Add(statusProgress) |> ignore

address.KeyPress.Add(fun args -> if (args.KeyChar = '\r') then browser.Url <- new Uri(address.Text))
go.Click.Add(fun _ -> browser.Url <- new Uri(address.Text))
toolbar.Items.Add(new ToolStripLabel("Address:")) |> ignore
toolbar.Items.Add(address) |> ignore
toolbar.Items.Add(go) |> ignore

browser.Navigating.Add(fun _ -> statusProgress.Visible <- true)
browser.DocumentCompleted.Add(fun _ -> statusProgress.Visible <- false
                                       address.Text <- browser.Url.AbsoluteUri)

form.Controls.Add(browser)
form.Controls.Add(toolbar)
form.Controls.Add(status)
form.PerformLayout()
form.Show()


Application.Run(form)


