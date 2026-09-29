INAB STUDIO – PROCEDURAL PROGRESS BARS  

Online Documentation:
 -- https://inabstudios.gitbook.io/procedural-progress-bars  

Discord: 
 -- https://discord.gg/K88zmyuZFD

---

COMPATIBILITY  
This asset supports Unity 6.0 and above.  
Fully compatible with both URP and HDRP.  
All shaders are built with Shader Graph for easy customization.

Supported Shader Types:  
- Canvas Shader – optimized for Unity UI elements.  
- Unlit Shader – designed for world space overlays and 3D usage.

---

IMPORT & SETUP

Before Importing (only required for demo scenes):  
1. Install Cinemachine  
   Window → Package Manager → Unity Registry → Cinemachine → Install

2. Enable Input Handling  
   Project Settings → Player → Other Settings → Active Input Handling → Input Manager (Old) or Both

3. Import TextMeshPro Essentials  
   Window → TextMeshPro → Import TMP Essential Resources

Importing the Asset:  
- Choose your render pipeline: URP.unitypackage or HDRP.unitypackage  
  Located in:  
  INab Studio/2D Assets/Procedural Progress Bars/

- Double-click the appropriate package to import.

---

DEMO SCENES  
Example scenes are located at:  
INab Studio/2D Assets/Procedural Progress Bars/Common [URP or HDRP]/Demo Scenes

Each scene includes:  
- A short overview of the content being demonstrated.  
- On-screen instructions for interaction and previewing functionality.

---

QUICK START: OVERVIEW

This asset revolves around three key components:  
- ProceduralProgressBar.cs – the main controller script.  
- Canvas Shader – for Unity UI Image elements.  
- Unlit Shader – for 3D world space meshes.  

All shaders are built with Shader Graph and function in both URP and HDRP.

Recommended usage:  
- Use the Canvas Shader for UI elements.  
- Use the Unlit Shader for 3D meshes (planes, quads, etc.).

---

ADDING A PROGRESS BAR

UI (Canvas-based) Setup:

1. Create the Material  
   - In Project window: Create > Material  
   - Set Shader to: Inab Studio/Procedural Progress Bar Canvas

2. Add a UI Image  
   - In your Canvas: Create UI → Image  
   - Assign the material you created to this Image

3. Set Image Dimensions  
   - Width and Height should be equal (e.g., 800x800) to prevent stretching  
   - Keep Scale at (1, 1, 1)  
   - Bar thickness can be adjusted through shader properties

4. Attach the Script  
   - Add the ProceduralProgressBar.cs component to the same GameObject

5. Assign the Material in the Script  
   - Either click “Get Material” or manually assign your custom material

6. Test and Adjust  
   - Use the Fill Amount property to preview animation  
   - Use built-in test buttons (available at runtime) to simulate fill behavior

7. Optional: Enable Help Boxes  
   - Leave “Enable Help Boxes” checked for in-editor tooltips and guidance

---

For complete documentation, examples, and advanced customization:  
https://inabstudios.gitbook.io/procedural-progress-bars
