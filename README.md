# FaceRecApp

A modular, console‐based facial recognition application in C# using OpenCvSharp.
It supports:

* **Training** on labeled face images (organized in `data/<personName>/`) to build an LBPH model.
* **Live recognition** from a webcam or RTSP/IP‐camera feed, with:

  * Haar‐cascade face detection
  * LBPH face recognition
  * CSRT‐based tracking between detections (for efficiency)
  * Rate‐limited terminal alerts when a known face appears
  * **Snapshot Service**: Automatically capture and save cropped images of recognized faces with overlaid names, logged to `snapshots/log.csv`.
  * **Avalonia GUI** for managing persons, training, and live stream with overlay alerts.

This README walks you through setup, configuration, training, running in console mode, and using the Avalonia GUI.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Prerequisites](#prerequisites)
3. [Installation & Build](#installation--build)
4. [Configuration (`config.json`)](#configuration-configjson)
5. [Training the Model (Console)](#training-the-model-console)
6. [Running Live Recognition (Console)](#running-live-recognition-console)
7. [Avalonia GUI](#avalonia-gui)
8. [How It Works (High‐Level)](#how-it-works-high-level)
9. [Folder & File Descriptions](#folder--file-descriptions)
10. [Extending & Customization](#extending--customization)
11. [Ignoring Sensitive Files (`.gitignore`)](#ignoring-sensitive-files-gitignore)
12. [License](#license)

---

## Project Structure

```
FaceRecApp/
├─ .gitignore
├─ README.md
├─ config.json
├─ cascades/
│   └─ haarcascade_frontalface_default.xml
├─ data/
│   └─ olamide/
│       ├─ 1.jpg
│       └─ 2.jpg
├─ FaceRecApp.csproj
├─ AppConfig.cs
├─ FaceTrainer.cs
├─ FaceRecognizerService.cs
├─ CameraService.cs
├─ AlertService.cs
├─ SnapshotService.cs
├─ Program.cs
├─ App.xaml
├─ App.xaml.cs
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
└─ (Optional) Styles.axaml
```

* **`.gitignore`**: Ensures no sensitive or bulky files (models, images, configs) are pushed.
* **`README.md`**: This file.
* **`config.json`**: Contains adjustable settings (paths, thresholds, camera source, etc.).
* **`cascades/`**: Holds the Haar‐cascade XML for face detection.
* **`data/`**: User‐supplied training images (each subfolder is one person).
* **`FaceRecApp.csproj`**: The project file.
* **`AppConfig.cs`**: Loads and deserializes `config.json`.
* **`FaceTrainer.cs`**: Scans `data/`, detects/crops faces, trains an LBPH model, outputs `trained_lbph_model.yml` and `labels.json`.
* **`FaceRecognizerService.cs`**: Loads the saved model & labels, and runs `Predict(...)` on cropped faces.
* **`CameraService.cs`**: Wraps `VideoCapture` (webcam or RTSP URL).
* **`AlertService.cs`**: Rate‐limits terminal alerts per recognized person.
* **`SnapshotService.cs`**: Captures and saves cropped images of recognized faces, overlaying the name and logging details.
* **`Program.cs`**: Orchestrates command‐line arguments (`train`, `camera`, `gui`), detection, recognition, tracking, alerts, and invokes snapshot service.
* **`App.xaml` & `App.xaml.cs`**: Bootstraps the Avalonia application and sets global theme.
* **`MainWindow.xaml` & `MainWindow.xaml.cs`**: Avalonia UI for Add Person, Train, and Live Stream tabs.
* **`Styles.axaml`** (optional): If you prefer splitting out styles from `MainWindow.xaml`. Otherwise, styles reside in `MainWindow.xaml`.

---

## Prerequisites

* **.NET SDK 6.0+** (tested on .NET 9.0)

  ```bash
  dotnet --version
  # Expect something like 9.0.x
  ```
* **Homebrew** (on macOS)
* **OpenCV (via Homebrew)**

  ```bash
  brew install opencv
  ```
* **OpenCvSharp4** NuGet packages (added below)
* **A webcam or RTSP-capable IP/CCTV camera**
* **C#-capable IDE or editor** (e.g. Visual Studio Code)

---

## Installation & Build

1. **Clone or download this repo** into a folder on your machine.

2. **Open a terminal** and navigate into the project folder:

   ```bash
   cd /path/to/FaceRecApp
   ```

3. **Install NuGet dependencies**:

   ```bash
   dotnet add package OpenCvSharp4
   dotnet add package OpenCvSharp4.runtime.osx_arm64 --prerelease
   dotnet restore
   ```

   * `OpenCvSharp4` is the .NET wrapper.
   * `OpenCvSharp4.runtime.osx_arm64` provides the native `libOpenCvSharpExtern.dylib` for M1/M2 Macs.

4. **Copy the Haar cascade file** (if not already present):

   ```bash
   mkdir -p cascades
   cp "$(brew --prefix opencv)/share/opencv4/haarcascades/haarcascade_frontalface_default.xml" cascades/
   ```

5. **Verify native library presence**:

   ```bash
   ls bin/Debug/net*/runtimes/osx-arm64/native/libOpenCvSharpExtern.dylib
   ```

   You should see `libOpenCvSharpExtern.dylib` under your build output.

6. **Build the project**:

   ```bash
   dotnet build
   ```

---

## Configuration (`config.json`)

`config.json` holds all paths and tunable thresholds. Example:

```json
{
  "cascadeFile":        "cascades/haarcascade_frontalface_default.xml",
  "dataFolder":         "data",
  "modelFile":          "trained_lbph_model.yml",
  "labelsFile":         "labels.json",
  "recognitionThreshold": 70,
  "minFaceSize":        50,
  "frameSkip":          2,
  "cameraSourceRaw":    "0",
  "alertCooldownSeconds": 10
}
```

* **`cascadeFile`**: Path to Haar cascade XML (relative to project root).
* **`dataFolder`**: Parent folder where each subfolder is a person’s images.
* **`modelFile`**: Output path for the LBPH model (YAML format).
* **`labelsFile`**: Output path for the label-to-name JSON mapping.
* **`recognitionThreshold`**: LBPH confidence threshold (lower = more confident).
* **`minFaceSize`**: Minimum pixel height/width of a face to consider.
* **`frameSkip`**: Perform full `DetectMultiScale(...)` only every *N* frames.
* **`cameraSourceRaw`**: `"0"` or `"1"` for webcam index, or an RTSP URL (`"rtsp://..."`).
* **`alertCooldownSeconds`**: Seconds before re-alerting for the same person.

> **Tip:** If your camera feed has credentials or sensitive info, do **not** commit `config.json`—add it to `.gitignore` and keep a local copy.

---

## Training the Model (Console)

1. **Prepare your training data**
   Create subfolders under `data/`, one per person. Example:

   ```
   data/
   ├─ olamide/
   │   ├─ 1.jpg
   │   └─ 2.jpg
   ├─ alice/
   │   ├─ a1.png
   │   └─ a2.png
   └─ bob/
       ├─ b1.jpg
       └─ b2.jpg
   ```

   * Place 5–10 clear, front-facing face images per person (JPG or PNG).
   * You do **not** need to crop faces manually; the trainer does it via Haar cascade.

2. **Train the model**

   Ensure native libraries are visible:

   ```bash
   export DYLD_FALLBACK_LIBRARY_PATH="$PWD/bin/Debug/net*/runtimes/osx-arm64/native:$PWD/bin/Debug/net*:/usr/local/opt/opencv/lib"
   ```

   Then run:

   ```bash
   dotnet run -- train
   ```

   * The `train` argument triggers `FaceTrainer.Train()`.
   * Output example:

     ```
     🔄 Training mode selected.
     ✅ Model saved to 'trained_lbph_model.yml' (faces: 15).
     ✅ Labels saved to 'labels.json'.
     ```

---

## Running Live Recognition (Console)

After you’ve trained the model, run:

```bash
export DYLD_FALLBACK_LIBRARY_PATH="$PWD/bin/Debug/net*/runtimes/osx-arm64/native:$PWD/bin/Debug/net*:/usr/local/opt/opencv/lib"

dotnet run -- camera
```

* A window titled **“Face Detection & Recognition”** opens, showing your camera feed.
* Each frame:

  1. **CSRT trackers** update existing face locations (green).
  2. Every `frameSkip` frames, run fresh `DetectMultiScale(...)` + `Predict(...)`:

     * If recognized, draw a lime‐green box + name, start a new tracker, save a snapshot in `snapshots/`, and log to `snapshots/log.csv`.
     * Otherwise draw a red “Unknown” box.
* Press **ESC** inside the OpenCV window to quit.

---

## Avalonia GUI

You can manage persons, retrain, and view live recognition all from a desktop app. To launch:

```bash
export DYLD_FALLBACK_LIBRARY_PATH="$PWD/bin/Debug/net*/runtimes/osx-arm64/native:$PWD/bin/Debug/net*:/usr/local/opt/opencv/lib"

dotnet run -- gui
```

This opens an Avalonia window with three tabs:

1. **Add Person**

   * Lists existing subfolders under `data/`.
   * Enter a new name, click **Add Person** to create `data/<name>/`.
   * Select a folder, click **Add Images** to browse and copy JPG/PNG files.
   * Status messages appear below.

2. **Train Model**

   * Click **Start Training** to retrain LBPH in the background.
   * Progress/status appears below the button.
   * After training, the live stream will use the updated model immediately.

3. **Live Stream**

   * Displays your camera feed in the right pane.
   * Face detection, recognition, and CSRT tracking run in real time:

     * Green boxes track existing faces.
     * Lime-green boxes + names for newly recognized faces.
     * Red boxes + “Unknown” for unrecognized faces.
   * If a known face appears (and cooldown expired), a red alert banner overlays at the top for 3 seconds.
   * To quit, just close the window.

### Required Avalonia Files

1. **App.xaml**

   ```xml
   <Application xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                x:Class="FaceRecApp.App">

     <Application.Styles>
       <!-- Use built-in Fluent theme -->
       <FluentTheme Mode="Light"/>
     </Application.Styles>
   </Application>
   ```

2. **App.xaml.cs**

   ```csharp
   using Avalonia;
   using Avalonia.Controls.ApplicationLifetimes;
   using Avalonia.Markup.Xaml;

   namespace FaceRecApp
   {
       public partial class App : Application
       {
           public override void Initialize()
           {
               AvaloniaXamlLoader.Load(this);
           }

           public override void OnFrameworkInitializationCompleted()
           {
               if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
               {
                   desktop.MainWindow = new MainWindow();
               }
               base.OnFrameworkInitializationCompleted();
           }
       }
   }
   ```

> **Optional:** If you prefer separating styles from `MainWindow.xaml`, create `Styles.axaml` containing all `<Style>` definitions, and include it in `App.xaml` via `<StyleInclude Source="avares://FaceRecApp/Styles.axaml"/>`. Otherwise, keep styles embedded in `MainWindow.xaml`.

---

## How It Works (High‐Level)

1. **Detection**

   * `CascadeClassifier.DetectMultiScale(...)` on the grayscale frame, ignoring faces smaller than `minFaceSize`.
2. **Recognition**

   * Crop & resize each detected face → 200×200.
   * Run `LBPHFaceRecognizer.Predict(...)`. If `confidence < threshold`, return name; else “Unknown.”
3. **Tracking (CSRT)**

   * Each recognized face starts a `TrackerCSRT`. On subsequent frames, `tracker.Update(frame, ref rect)` is called instead of full detection.
   * If a tracker fails, it's removed, and fresh detection picks it up later.
4. **Snapshot Service**

   * On fresh detection of a recognized face, `SnapshotService.TakeSnapshot(...)` crops, overlays the name, saves a PNG under `snapshots/`, and appends `Timestamp,Name,Filename` to `snapshots/log.csv`.
5. **Alerting**

   * Whenever a known face (label ≥ 0) is detected (in detection or tracking), `AlertService` checks if `alertCooldownSeconds` have passed since the last alert for that label. If so, a red banner appears in the GUI or a message prints in console.

---

## Folder & File Descriptions

### `AppConfig.cs`

Loads and deserializes `config.json` into an `AppConfig` object.

### `FaceTrainer.cs`

* Scans each subfolder under `dataFolder`.
* For every `.jpg`/`.png`, loads as grayscale, runs `DetectMultiScale`, and crops the first face.
* Resizes face → 200×200, stores it with a numeric label.
* Trains an `LBPHFaceRecognizer` on all faces + labels, writes `modelFile` & `labelsFile`.

### `FaceRecognizerService.cs`

* Loads `trained_lbph_model.yml` and `labels.json`.
* Implements `Predict(Mat grayFrame, Rect faceRect)` → `(name, confidence, label)`.

### `CameraService.cs`

* Wraps `VideoCapture` so you pass either a webcam index (`"0","1"`) or an RTSP URL (`"rtsp://..."`).
* Provides `GrabFrame()`.

### `AlertService.cs`

* Rate-limits terminal or GUI alerts. Stores `label → last alert timestamp`.
* `ShouldAlert(label)` returns true if `alertCooldownSeconds` have passed since last alert.

### `SnapshotService.cs`

* On fresh detection of a recognized face, crops and saves a PNG in `snapshots/`, overlays the name, and logs to `snapshots/log.csv`.

### `Program.cs`

* Parses command line: `train`, `camera`, or `gui`.
* In **train**: runs `FaceTrainer.Train()`.
* In **camera**: runs the console-only camera loop.
* In **gui**: launches Avalonia desktop lifetime.

### `App.xaml` & `App.xaml.cs`

* Bootstraps the Avalonia application and sets a global Fluent theme.

### `MainWindow.xaml` & `MainWindow.xaml.cs`

* Avalonia UI with three tabs:

  1. **Add Person**: Create new subfolders under `data/` and copy images.
  2. **Train Model**: Retrain LBPH model from the UI thread.
  3. **Live Stream**: Shows live camera feed, detection, recognition, and overlay alerts.

---

## Extending & Customization

* **Thresholds**

  * Adjust `recognitionThreshold` for stricter/looser recognition.
  * Increase `frameSkip` if CPU is high.
* **RTSP Camera**

  * Set `cameraSourceRaw` to an RTSP URL with credentials.
* **Add People**

  * Create `data/<newPerson>/` with images, then `dotnet run -- train`.
* **Switch to DNN Detector**

  * Replace Haar cascade with `CvDnn` (e.g. ResNet‐SSD) in `FaceTrainer` & `Program`.
* **Label Metadata**

  * Extend `labels.json` to hold extra fields (role, dept). Modify `LabelEntry` accordingly.
* **GUI Enrollment**

  * Add a “Capture face from camera” button that grabs a frame, auto-detects and saves a new image in `data/<person>/`, then retrains.
* **Email/SMS Alerts**

  * Modify `AlertService` to send via SendGrid/Twilio instead of just showing a banner.

---

## Ignoring Sensitive Files (`.gitignore`)

```
# Build outputs
bin/
obj/

# IDE files
.vscode/
*.user
*.suo

# macOS-specific
.DS_Store

# Model & label files (recreated via train)
trained_lbph_model.yml
labels.json

# Config (may contain credentials)
config.json

# Training data
 data/

# Haar cascades (can be downloaded)
cascades/

# Logs & temp
*.log
*.tmp
*~

# Secrets
*.env
```

---

## License

This project is released under the Apache-2.0 license. Feel free to use.
