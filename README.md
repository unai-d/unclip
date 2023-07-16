# Unclip
Unclip is a Clip Studio Paint (CSP) project file reader capable of extracting layers to individual PNG files.

![Main window preview](/img/main-window-demo.png)

The project is divided in:

- The base library (`src/Unai.Unclip`) which contains the base class that parses CSP project files (`CspFile`), among others.
- A GTK front-end (`src/Unai.Unclip.Gui.Gtk`), shown in the above picture.

## Requirements

Unclip requires .NET Core 6.0 or later installed on your system.

## Disclaimer

Unclip is still very barebones and some functionality may not work properly.

## Building and running

Standard .NET commands apply for each project:

- To build the project, do <code>dotnet build</code>.
- To run the project (in the case of the front-end), do <code>dotnet run</code>.