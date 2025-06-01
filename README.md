# FaceRecApp

A modular, console‐based facial recognition application in C# using OpenCvSharp.
It supports:

* **Training** on labeled face images (organized in `data/<personName>/`) to build an LBPH model.
* **Live recognition** from a webcam or RTSP/IP‐camera feed, with:

  * Haar‐cascade face detection
  * LBPH face recognition
  * CSRT‐based tracking between detections (for efficiency)
  * Rate‐limited terminal alerts when a known face appears

This README walks you through setup, configuration, training, and running the app.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Prerequisites](#prerequisites)
3. [Installation & Build](#installation--build)
4. [Configuration (`config.json`)](#configuration-configjson)
5. [Training the Model](#training-the-model)
6. [Running Live Recognition](#running-live-recognition)
7. [How It Works (High‐Level)](#how-it-works-high-level)
8. [Folder & File Descriptions](#folder--file-descriptions)
9. [Extending & Customization](#extending--customization)
10. [Ignoring Sensitive Files (`.gitignore`)](#ignoring-sensitive-files-gitignore)
11. [License](#license)

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
└─ Program.cs
```

* **`.gitignore`**: Ensures no sensitive or bulky files (models, images, configs) are pushed.
* **`README.md`**: This file.
* **`config.json`**: Contains all adjustable settings (paths, thresholds, camera source, etc.).
* **`cascades/`**: Holds the Haar‐cascade XML for face detection.
* **`data/`**: User‐supplied training images (each subfolder is one person).
* **`FaceRecApp.csproj`**: The project file.
* **`AppConfig.cs`**: Loads and deserializes `config.json`.
* **`FaceTrainer.cs`**: Scans `data/`, detects/crops faces, trains an LBPH model, outputs `trained_lbph_model.yml` and `labels.json`.
* **`FaceRecognizerService.cs`**: Loads the saved model & labels, and runs `Predict(...)` on cropped faces.
* **`CameraService.cs`**: Wraps `VideoCapture` (webcam or RTSP URL).
* **`AlertService.cs`**: Rate‐limits terminal alerts per recognized person.
* **`Program.cs`**: Orchestrates “train” vs. “live” mode, manages detection, recognition, tracking, and alerts.

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
* **A webcam or RTSP‐capable IP/CCTV camera**
* **C#‐capable IDE or editor** (e.g. Visual Studio Code)

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
* **`labelsFile`**: Output path for the label‐to‐name JSON mapping.
* **`recognitionThreshold`**: LBPH confidence threshold (lower = more confident).
* **`minFaceSize`**: Minimum pixel height/width of a face to consider.
* **`frameSkip`**: Perform full `DetectMultiScale(...)` only every *N* frames.
* **`cameraSourceRaw`**: `"0"` or `"1"` for webcam index, or an RTSP URL (`"rtsp://..."`).
* **`alertCooldownSeconds`**: Seconds before re‐alerting for the same person.

> **Tip:** If your camera feed has credentials or sensitive info, do **not** commit `config.json`—add it to `.gitignore` and keep a local copy.

---

## Training the Model

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

   * Place 5–10 clear, front‐facing face images per person (JPG or PNG).
   * You do **not** need to crop faces manually; the trainer does it via Haar cascade.

2. **Build & train**
   In your terminal (ensure native libraries are visible—see next step):

   ```bash
   # Only needed once per new Terminal session:
   export DYLD_FALLBACK_LIBRARY_PATH="$PWD/bin/Debug/net*/runtimes/osx-arm64/native:$PWD/bin/Debug/net*:/usr/local/opt/opencv/lib"

   dotnet build
   dotnet run train
   ```

   * The `train` argument triggers `FaceTrainer.Train()`.
   * You’ll see console output like:

     ```
     🔄 Training mode selected.
     ✅ Model saved to 'trained_lbph_model.yml' (faces: 15).
     ✅ Labels saved to 'labels.json'.
     ```
   * Two new files appear:

     * **`trained_lbph_model.yml`** (the LBPH model)
     * **`labels.json`** (mapping each label ID → person name)

> **Note:** If you add more people/images later, simply rerun `dotnet run train` to rebuild.

---

## Running Live Recognition

Once the model and labels exist, start live recognition:

```bash
# If you restarted Terminal, re‐export the fallback path:
export DYLD_FALLBACK_LIBRARY_PATH="$PWD/bin/Debug/net*/runtimes/osx-arm64/native:$PWD/bin/Debug/net*:/usr/local/opt/opencv/lib"

dotnet run
```

* A window titled **“Face Detection & Recognition”** opens, showing your camera feed.
* Each frame:

  1. **CSRT trackers** update existing face locations (drawn in green).
  2. Every *`frameSkip`* frames, run a fresh Haar detection + LBPH recognition:

     * If recognized (`confidence < threshold`), draw a lime‐green box + name, start a new CSRT tracker, and print a terminal alert (once per `alertCooldownSeconds`).
     * Otherwise draw a red “Unknown” box.
* **Press ESC** inside the window to quit.

Example terminal alert:

```
⚠️ Alert: olamide detected at 09:22:53 PM
```

---

## How It Works (High‐Level)

1. **Detection**

   * Uses `CascadeClassifier.DetectMultiScale(...)` on the grayscale frame, ignoring faces smaller than `minFaceSize`.
2. **Recognition**

   * Crops and resizes each detected face to 200×200.
   * Runs `LBPHFaceRecognizer.Predict(...)`.
   * If `confidence < recognitionThreshold`, returns the corresponding name; otherwise “Unknown.”
3. **Tracking (CSRT)**

   * For each newly recognized face, a `TrackerCSRT` is initialized on that bounding box.
   * On subsequent frames, we call `tracker.Update(frame, ref rect)` instead of running full detection—much faster.
   * If a tracker fails (drifts off), we remove it and fall back to detection.
4. **Alerting**

   * Whenever a face is recognized (label ≥ 0) either in detection or in tracker update, `AlertService` checks if at least `alertCooldownSeconds` have passed since the last alert for that label. If yes, a terminal alert is printed.

---

## Folder & File Descriptions

### `AppConfig.cs`

Loads and deserializes `config.json` into a strongly‐typed `AppConfig` object.

### `FaceTrainer.cs`

* Scans each subfolder under `dataFolder`.
* For every `.jpg`/`.png` inside, loads the image in grayscale, runs `DetectMultiScale`, and crops the first face found.
* Resizes each face to 200×200, stores it with a numeric label.
* Trains an `LBPHFaceRecognizer` on all faces + labels, writes `modelFile` (YAML) and `labelsFile` (JSON).

### `FaceRecognizerService.cs`

* Loads the LBPH model (`trained_lbph_model.yml`) and the label map (`labels.json`).
* Exposes `Predict(Mat grayFrame, Rect faceRect)` → `(name, confidence, label)`, clipping the rectangle to image bounds to avoid out‐of‐range errors.

### `CameraService.cs`

* Wraps `VideoCapture` so you can pass either a numeric index (`"0"`, `"1"`) or an RTSP URL (`"rtsp://..."`).
* Exposes `GrabFrame()` to fetch the next frame as a `Mat`.

### `AlertService.cs`

* Rate‐limits alerts per label. Stores a dictionary `label → last alert Timestamp`.
* `ShouldAlert(label)` returns true if more than `alertCooldownSeconds` have passed since the last alert, and updates the timestamp.

### `Program.cs`

* Parses command‐line arguments (`"train"` vs. default).
* In **train** mode: invokes `FaceTrainer.Train()`.
* In **live** mode:

  1. Loads `FaceRecognizerService`, `CameraService`, and `AlertService`.
  2. Loops over frames:

     * Updates each `TrackerCSRT` in `trackers[]`, draws tracked boxes, and triggers alerts if needed.
     * Every `frameSkip` frames, runs a fresh `DetectMultiScale` + `Predict(...)`.
     * Initializes new trackers for recognized faces.
  3. Quits on **ESC**.

---

## Extending & Customization

* **Adjust thresholds** in `config.json`:

  * Lower `"recognitionThreshold"` (e.g. 60) to require higher similarity.
  * Increase `"frameSkip"` (e.g. 3 or 4) if CPU usage is high.
* **Use an RTSP camera**:
  Set `"cameraSourceRaw": "rtsp://username:password@192.168.1.10:554/Streaming/Channels/101"`.
* **Add more people**:

  * Create `data/<newPerson>/` with at least 5 face images.
  * Rerun `dotnet run train`.
* **Swap to a DNN‐based face detector**:
  Replace `CascadeClassifier` calls in `FaceTrainer` and `Program` with `CvDnn` modules (e.g. ResNet‐SSD).
* **Use JSON for label metadata**:
  `labels.json` can store extra fields (e.g. `"role"`, `"department"`). Extend `LabelEntry` to accommodate.
* **Implement GUI enrollment**:
  Let a user press a hotkey to “enroll” a new face from the live feed and run training in the background.
* **Email/SMS alerts**:
  Modify `AlertService` to send an email or SMS (via SendGrid/Twilio) instead of just printing to the console.

---

## Ignoring Sensitive Files (`.gitignore`)

Ensure you don’t accidentally commit large or sensitive files. Example `.gitignore`:

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

# Configuration (may contain RTSP credentials)
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

This project is released under the MIT License. Feel free to use, modify, and distribute.

---

**Enjoy building your own facial recognition service!**

```
```
