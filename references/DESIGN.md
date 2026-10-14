---
name: Obsidian Vector Studio
colors:
  surface: '#121316'
  surface-dim: '#121316'
  surface-bright: '#38393c'
  surface-container-lowest: '#0d0e11'
  surface-container-low: '#1b1b1f'
  surface-container: '#1f1f23'
  surface-container-high: '#292a2d'
  surface-container-highest: '#343538'
  on-surface: '#e3e2e6'
  on-surface-variant: '#c7c4d8'
  inverse-surface: '#e3e2e6'
  inverse-on-surface: '#2f3034'
  outline: '#918fa1'
  outline-variant: '#464555'
  surface-tint: '#c3c0ff'
  primary: '#c3c0ff'
  on-primary: '#1d00a5'
  primary-container: '#4f46e5'
  on-primary-container: '#dad7ff'
  inverse-primary: '#4d44e3'
  secondary: '#ffb3ad'
  on-secondary: '#68000a'
  secondary-container: '#a40217'
  on-secondary-container: '#ffaea8'
  tertiary: '#7bd0ff'
  on-tertiary: '#00354a'
  tertiary-container: '#00678c'
  on-tertiary-container: '#b2e1ff'
  error: '#ffb4ab'
  on-error: '#690005'
  error-container: '#93000a'
  on-error-container: '#ffdad6'
  primary-fixed: '#e2dfff'
  primary-fixed-dim: '#c3c0ff'
  on-primary-fixed: '#0f0069'
  on-primary-fixed-variant: '#3323cc'
  secondary-fixed: '#ffdad7'
  secondary-fixed-dim: '#ffb3ad'
  on-secondary-fixed: '#410004'
  on-secondary-fixed-variant: '#930013'
  tertiary-fixed: '#c4e7ff'
  tertiary-fixed-dim: '#7bd0ff'
  on-tertiary-fixed: '#001e2c'
  on-tertiary-fixed-variant: '#004c69'
  background: '#121316'
  on-background: '#e3e2e6'
  surface-variant: '#343538'
typography:
  headline-lg:
    fontFamily: Inter
    fontSize: 1.25rem
    fontWeight: '600'
    lineHeight: 1.5rem
    letterSpacing: -0.015em
  headline-md:
    fontFamily: Inter
    fontSize: 1rem
    fontWeight: '600'
    lineHeight: 1.25rem
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 0.875rem
    fontWeight: '600'
    lineHeight: 1.125rem
    letterSpacing: -0.005em
  body-md:
    fontFamily: Inter
    fontSize: 0.8125rem
    fontWeight: '400'
    lineHeight: 1.125rem
    letterSpacing: 0em
  body-sm:
    fontFamily: Inter
    fontSize: 0.75rem
    fontWeight: '400'
    lineHeight: 1rem
    letterSpacing: 0em
  label-md:
    fontFamily: Inter
    fontSize: 0.75rem
    fontWeight: '500'
    lineHeight: 0.875rem
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Inter
    fontSize: 0.6875rem
    fontWeight: '500'
    lineHeight: 0.75rem
    letterSpacing: 0.02em
  code-sm:
    fontFamily: JetBrains Mono
    fontSize: 0.75rem
    fontWeight: '400'
    lineHeight: 1rem
    letterSpacing: -0.01em
  code-xs:
    fontFamily: JetBrains Mono
    fontSize: 0.6875rem
    fontWeight: '400'
    lineHeight: 0.875rem
    letterSpacing: 0em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  gutter: 0.5rem
  margin: 0.5rem
  space-xs: 0.25rem
  space-sm: 0.375rem
  space-md: 0.5rem
  space-lg: 0.75rem
  space-xl: 1rem
---

## Brand & Style

This design system targets technical professionals, document architects, prepress operators, and developers who manipulate PDF structure, vector paths, and document typography. 

The aesthetic synthesizes modern utility-first desktop software (Linear, JetBrains Fleet, VS Code, Figma) with native desktop windowing conventions across Windows 11 and Linux. Visual noise is eliminated to foreground the document canvas. The interface expresses speed, surgical accuracy, and deep tool density without visual clutter. 

Tactile affordances rely on crisp 1px borders, subtle surface luminosity shifts, and deliberate micro-interactions rather than heavy shadows or ornamental skeuomorphism. Interactive chrome recedes into deep obsidian carbon, giving total visual prominence to SkiaSharp-rendered document elements.

## Colors

The palette uses a stepped value scale of deep carbon-slate neutrals calibrated for OLED and HDR workstation displays.

### Role Assignments
- **Neutral Canvas (`#121316`)**: Lowest tier background; used for the primary viewport frame housing the document canvas, main window background, and status bar.
- **Surface Tier 1 (`#1a1b1e`)**: Primary docking panels, collapsible sidebars, command ribbon, and contextual inspector backdrops.
- **Surface Tier 2 (`#24262b`)**: Input controls, tool switches, property cards, dropdown menus, and popovers.
- **Surface Tier 3 / Hover (`#2e3138`)**: Hover states for list rows, active tool toggle states, button hover backgrounds, and subtle dividers.
- **Structural Outlines (`#2e3138` / `#3f434d`)**: 1px borders separating panels, tool groups, and framing input boxes.
- **Primary Indigo (`#4f46e5`)**: Active selection indicators, primary action commits, focus rings, vector path handles, and active inspector tabs.
- **Secondary Crimson (`#ef4444`)**: PDF file-type badges, structural validation errors, destruction alerts (delete page), and standard PDF standard markers.
- **Tertiary Cyan (`#38bdf8`)**: Measurements, guide rulers, snapping alignment lines, and AST node type tags.

### Text Contrast Tiers
- **Primary Text (`#f8fafc`)**: Headings, selected tree items, active input values, coordinate readings.
- **Muted Body (`#94a3b8`)**: Property labels, unselected tree nodes, toolbar labels, secondary metrics.
- **Tertiary / Dimmed (`#64748b`)**: Shortcut annotations, disabled states, empty view placeholders, inactive rule tick marks.

## Typography

The typography scale is compressed to support intense data density without sacrificing readability on non-Retina or Linux high-DPI displays.

- **Primary Sans (Inter)**: Handles all standard UI text, panel titles, inspector inputs, and tooltips. Tabular figures (`tnum`) must be enabled globally for all numeric inputs and alignment controls.
- **Monospaced Utility (JetBrains Mono)**: Reserved for low-level PDF operator streams, hexadecimal color values, geometric coordinate displays (X, Y, W, H, Rotation), page index meters, and AST node identifiers.
- **Casing & Hierarchy**: Property labels in side panels use concise title-case or sentence-case at `label-sm` with `muted body` coloring. Section dividers within inspectors use uppercase `label-sm` with a tracking value of `0.05em`.

## Layout & Spacing

The layout is built around a non-fluid, fixed-docking workstation grid with a strict 4px base increment.

### Screen Partitioning & Shell Architecture
1. **Top App Bar / Contextual Ribbon (`height: 40px` / `height: 48px` extended)**: 
   - Hosts window controls, global document actions (Save, Export, Preflight), undo/redo stacks, mode switches (Annotate, Vector Edit, Redact, Form Builder), and zoom controls.
2. **Left Panel (`width: 240px` - `360px`, user-resizable, collapsible)**:
   - Houses tabbed modes: Page Thumbnails, Document Outline/Bookmarks, Annotations List, and low-level Content Stream/AST Object Tree.
3. **Central Viewport (SkiaSharp Canvas, auto-expanding)**:
   - Houses canvas rulers (top/left `16px`), pan-and-zoom viewport, page margin guides, and multi-page artboard staging area.
4. **Right Inspector (`width: 280px` - `340px`, user-resizable, collapsible)**:
   - Contains accordion-style inspection modules: Transform/Geometry, Fill/Stroke, Font/Typography, Action Triggers, and PDF Metadata.
5. **Bottom Status Bar (`height: 24px`)**:
   - Fixed height strip displaying: document conformance level (PDF/A-2b), color profile (CMYK/RGB), cursor position `(X, Y) pt`, zoom percentage slider, and document preflight status indicator.

### Panel Spacing Discipline
- Compact inputs maintain `height: 24px` or `height: 28px`.
- Inter-property row gaps: `space-xs` (4px).
- Module padding: `space-md` (8px).
- Panel separation: strict 1px solid dividers using `#2e3138`, avoiding margins between panels.

## Elevation & Depth

Visual hierarchy uses tonal surface layering and low-contrast borders rather than diffuse drop shadows.

### Elevation Hierarchy
- **Base Layer (`#121316`)**: Canvas host viewport and global window framing.
- **Docked Surface Layer (`#1a1b1e`)**: Left/Right utility sidebars, Ribbon, and Bottom Status Bar. Separated from the base layer by a 1px solid `#2e3138` border.
- **Inset Controls (`#16171a`)**: Text input fields, numeric steppers, and viewport canvas background when in multi-page layout mode.
- **Raised Controls (`#24262b`)**: Standard push buttons, segmented control tabs, dropdown triggers. Border: 1px `#3f434d`.
- **Floating Overlays & Popovers (`#24262b`)**: Context menus, floating quick-tools, and color pickers. Uses a high-contrast border (1px `#4f46e5` at 40% opacity or `#3f434d`) accompanied by a subtle ambient shadow: `box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45)`.
- **Active Focus Rings**: 1px crisp outline in `#4f46e5` with no outer glow spread.

## Shapes

The design uses tight, purposeful radii that fit dense desktop productivity suites.

- **Inputs, Buttons, and Selectors**: `rounded` (0.25rem / 4px).
- **Cards, Floating Inspector Palettes, and Modals**: `rounded-lg` (0.5rem / 8px).
- **Toolbar Groups & Segmented Button Strips**: `rounded` (0.25rem / 4px) wrapper with internal dividers.
- **Canvas Selection Handles**: Precise squares (`0px` radius) or tiny circles (`rounded-full`) at `6px` diameter with 1px border.
- **Pill Badges**: `rounded-full` reserved solely for status indicators, PDF standard chips, and notification dots.

## Components

### Buttons
- **Default/Ghost**: Transparent background, text `#94a3b8`, 1px transparent border. Hover: background `#2e3138`, text `#f8fafc`.
- **Raised Utility**: Background `#24262b`, text `#f8fafc`, 1px border `#2e3138`. Active: background `#1a1b1e`.
- **Primary Indigo**: Background `#4f46e5`, text `#ffffff`, border-free. Hover: `#4338ca`. Focused: 1px `#f8fafc` offset ring.
- **Icon Buttons (Ribbon/Canvas)**: 28x28px box, centered 16px vector icon. Toggle states use `#2e3138` background with an indigo indicator bar (2px) at the base.

### Input Fields & Steppers
- **Compact Property Inputs**: Height 24px. Background `#16171a`, border 1px solid `#2e3138`, padding horizontal 6px. Font: `code-sm`.
- **Labeled Dimension Pairs (X/Y, W/H)**: Two inputs locked in an inline group with an uneditable prefix label (e.g., `X:`, `Y:`) colored in `tertiary` (`#64748b`). Numeric values scrub on drag over the label.

### Tree Views & Thumbnail Lists
- **Document AST / Object Tree**: 22px row height. Monospace node tags (`/Catalog`, `/Pages`, `/Annot`). Indentation: 12px per tier. Chevron toggle rotates 90 degrees on expand. Selected row: background `#4f46e5` at 20% opacity with a 2px solid left edge in `#4f46e5`.
- **Page Thumbnails**: Card wrapper `#1a1b1e` with subtle 1px border `#2e3138`. Aspect ratio matches paper format (A4/Letter). Active page indicated by a solid 2px outline in `#4f46e5`. Page number pill centered below thumbnail.

### Checkboxes, Radio Buttons & Toggles
- **Checkbox**: 14x14px square, radius 3px, border 1px `#3f434d`. Checked: background `#4f46e5`, border `#4f46e5`, white checkmark icon.
- **Segmented Radio Toggle**: Unified pill enclosure (`#16171a`), inner sliding pill (`#2e3138`) indicating active alignment (Left, Center, Right, Justified).

### Inspector Modules
- Accordion cards with header row `28px` height. Header holds a chevron icon, uppercase section title in `label-sm`, and an optional right-aligned action icon (e.g., Reset, Add Property).
- Content drawer opens without pushing neighboring panels off-canvas; nested in a custom Avalonia `ScrollViewer` with slim 4px overlay scrollbars.

### PDF-Specific Indicators
- **PDF Conformance Tag**: Height 18px, padding horizontal 6px, radius 3px. Background `rgba(239, 68, 68, 0.12)`, text `#ef4444`, font `code-xs`, tracking `0.04em`.
- **Canvas Bounding Box**: 1px hairline in `#4f46e5` with 8 rectangular corner/edge transformation handles (`6x6px`, fill `#ffffff`, border 1px solid `#4f46e5`).