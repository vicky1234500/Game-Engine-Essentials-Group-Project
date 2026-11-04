# Single Sliding Sci-Fi Door with Stencil Masking System

*(Visible from one side --- ideal for top-down 3D games with
room-by-room loading)*

## Description

A **futuristic sliding door** with a **stencil-based masking system**,
perfect for top-down sci-fi games.
The door automatically hides behind its frame when it moves outside the
visible area --- no extra setup required.

------------------------------------------------------------------------

## Visual Features

- **2048×2048 PBR textures** --- Albedo, Normal, Metallic, AO
- **Futuristic sci-fi design** suitable for tech environments
- **Compatible with baked and realtime lighting** (lightmaps, probes)

------------------------------------------------------------------------

## Package Includes

- **1 Door model (FBX)**
- **2 Prefabs** --- open left / open right
- **2 Custom shaders** for URP
- **7 Ready-to-use materials**
- 🖼**12 Textures** (door, frame, sill, decorative holders)
- **Demo scene** with ready example setup

------------------------------------------------------------------------

## Technical Info

- Total: **1,452 tris**
- UVs: **Non-overlapping, optimized for baking**
- **UV2 channel** included for lightmaps
- Scale: 1 Unity unit = 1 meter

------------------------------------------------------------------------

## How to Use

1. **Import** the package into a URP project\
2. **Drag** `Door_OpenLeft` or `Door_OpenRight` prefab into your scene
3. **Set up** simple open/close animation (can be keyframed or
   scripted)
4. **Stencil masking** works automatically --- no extra setup needed

------------------------------------------------------------------------

## Masking System Overview

**Shaders:**

- `LitStencil_1_Equal` --- main door shader with stencil testing
- `MaskStencil_1_Always_Replace` --- mask shader defining the visible
  area

**Materials:**

- `M_Doors_Stencil` --- main door material with stencil masking
- `M_Doors_BlackBack_Stencil` --- back side of the door (non-visible
  side)
- `M_Frame_ClipMask_Stencil` --- frame mask material
- `M_Frame` --- frame material
- `M_FrameHolders` --- holders material
- `M_FrameSill` --- sill material
- `M_BlackMate` --- matte black base

------------------------------------------------------------------------

## Tags

Sci-Fi, Door, Stencil, URP, Shader, Masking, Sliding, Futuristic, PBR
