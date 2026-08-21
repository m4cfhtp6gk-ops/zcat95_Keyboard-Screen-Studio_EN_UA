# Keyboard Screen Studio design system

Version: 1.0  
Scope: the Windows control-side interface, the device preview component, and
any pages or dialogs added later.

## 1. Principles

1. **Restrained, clear, predictable.** The interface configures a device; it
   does not stack decorative icons to carry information. Primary actions rely
   on clear wording, hierarchy and state.
2. **Same meaning, same shape.** Controls with the same semantics must use the
   same template, size, states and text styles. A page must never fork its own
   near-identical variant.
3. **4 px grid.** Sizes and spacing are multiples of 4 px. The only exceptions
   are 1 px strokes and the locked device model.
4. **Content first.** Explanatory text is at most two lines. Key information
   must never depend on light colours, low contrast, or colour alone.
5. **Honest preview.** The device preview keeps the exact 142:428 aspect ratio,
   the firmware occlusions and the physical frame relationship. It is never
   stretched to suit the page layout.
6. **Settings apply themselves.** There is no persistent "save" or "push"
   button; theme and setting changes save, re-render and push automatically.
   Continuous typing and slider drags are debounced by 450 ms, and the final
   value must never be lost.
7. **Follow Windows conventions.** Minimise, close, tray residency and restore
   must preserve the window's original size and maximised state. The tray menu
   must always offer open, refresh-and-push, and exit.

## 2. Design tokens

All tokens live in `src/KeyboardScreen.App.Avalonia/Styles/DesignSystem.axaml`.
Pages must not introduce semantically duplicate colours, radii or control
templates.

### 2.1 Type

- One font stack for the application UI: Segoe UI Variable Text, Segoe UI,
  Microsoft YaHei UI. Only Windows system fonts are referenced; no font files
  are shipped in the source, the installer or the portable build.
- Windows, navigation, buttons, inputs, drop-downs, tips and dialogs all
  inherit that stack.
- Weights: Regular 400 for helper text, body copy and controls; SemiBold 600
  for group titles, page titles, the selected navigation item and key figures.
  Synthetic bold is not allowed.
- Sizes: 11 (helper labels), 12 (descriptions), 13 (controls), 15 (card
  titles), 20 (page titles), 25 (application feature titles).
- The only exception: the live font preview on the font page, and the keyboard
  screen rendering itself, use the font the user selected. A user font must
  never leak into the rest of the control-side UI.
- **Editable text boxes take no vertical padding.** TextBox and PasswordBox set
  horizontal padding only (top and bottom must be 0) and use
  `VerticalContentAlignment="Center"`. The editing viewport measures line boxes
  differently from TextBlock: vertical padding squeezes PART_ContentHost and
  can clip a CJK fallback font down to a horizontal line.
- Pages must not override the input template, and must not re-introduce
  non-zero vertical padding on an individual input. Standard input heights are
  36 px, 40 px and 44 px only. When using a compact height, set Height and
  MinHeight to the same value so the shared MinHeight=44 cannot clip a second
  time. All three heights must reuse the shared template in DesignSystem.axaml.
- After changing an input's font, size, height, padding or PART_ContentHost,
  run the headless UI smoke test. The real ink height of "北京" and "Ag09" must
  be complete — a successful compile is not acceptance.
- Fixed-format fields must be verified under their real parent constraints
  against "full content + caret + padding on both sides". Do not give a
  star-column child a MinWidth beyond the available column width. The accent
  colour row is measured against a 243 px minimum content width: a 32 px colour
  dot, 8 px spacing, an 88 px button, leaving the HEX input 107 px. The UI
  smoke test must check all three at once: seven characters visible, the
  trailing caret not clipped, and no overlap between the input and the button.

### 2.2 Corner radii

- `Radius.Window = 20`: the main content container and modal windows.
- `Radius.Card = 16`: information cards and the preview sidebar.
- `Radius.Control = 12`: buttons, inputs, drop-downs, navigation items.
- `Radius.Small = 8`: small tags and value boxes.
- `Radius.Toggle = 11`: the switch capsule.
- Round status dots, colour swatches and switch thumbs are full circles, not
  the generic radius.
- Never apply one radius to every control.

### 2.3 Spacing and sizing

- Page margin: 24.
- Content column spacing: 24.
- Card padding: 20.
- Card spacing: 16.
- Title to description: 8.
- Form field spacing: 12.
- Standard control height: 44. Compact buttons: 36. Title-bar buttons: 46 × 40.
- No clickable area smaller than 36 × 36.

### 2.4 Colour

- Backgrounds and surfaces use the `Surface.*` semantic brushes only.
- Text uses `Text.Primary`, `Text.Secondary` and `Text.Disabled` only.
- Strokes use `Stroke.Default`, `Stroke.Strong` and `Stroke.Focus` only.
- Control-side interaction is white and neutral grey throughout; it does not
  follow the keyboard screen's accent colour.
- The accent colour affects the keyboard screen only. Never let the user's
  choice spread to control-side buttons, navigation or focus states.
- Error, success and warning states must always carry text. Colour alone is not
  enough.

## 3. Components

### 3.1 Buttons

- Buttons are a white surface with dark text and a 1 px neutral grey stroke.
  No blue fills.
- Secondary: white or translucent surface, 1 px stroke, dark text.
- Hover, Pressed, Disabled and KeyboardFocus come from the shared template.
  Hover and Pressed change only the light-grey level.
- No system dotted focus rectangle; keyboard focus uses a neutral grey stroke.
- A button's height does not change with its label, and icons are not stretched
  to fill empty space.

### 3.2 Inputs and drop-downs

- Height 44, radius 12, 1 px stroke.
- Hover strengthens the stroke; Focus uses a 2 px blue stroke.
- The drop-down panel uses the same radius and font as the input. It must never
  fall back to the grey square-cornered system template.
- Paths and long text are truncated; the full value is available through a
  ToolTip or status text.

### 3.3 Switches, sliders and scrollbars

- Boolean settings use a 38 × 22 switch. A round check box is not a switch.
- Slider track 4 px, thumb 16 px; the value is shown in its own small value box.
- Scrollbar width 10 px, thumb 6 px, rounded ends, no system arrow buttons.

### 3.4 Cards and navigation

- Cards use `SectionCard`. Never write a background colour or radius directly
  in a page.
- Top-level navigation expresses page switching only. The selected state is a
  light grey surface with dark text.
- The theme list on the left expresses the display theme only. Its selected
  state uses a blue stroke, not a blue fill with black text.
- The theme list stays single-level, with a fixed order and short names. No
  category headers, favourites, recents or search.
- Titles, descriptions and controls line up on a shared baseline.

### 3.5 Colour picker

- Never call the legacy WinForms `ColorDialog`.
- Use the in-app dialog with HEX, RGB sliders, a colour preview and common
  colours.
- Preview live while editing; only "Apply colour" commits, and cancelling must
  not contaminate the settings.

### 3.6 System tray and window state

- "Minimise to tray", "close to tray" and "start minimised" are configured
  separately and never implicitly override each other.
- Double-clicking the tray icon restores the window, including the
  Normal/Maximized state it had before it was hidden.
- With close-to-tray enabled, the title-bar close button only hides the window.
  Only "Exit" in the tray menu ends the process.
- The tray menu always contains: open, refresh and push, exit.
- Neither the tray icon nor a hidden window may stop collection, auto-save or
  the scheduled push.

## 4. Locked component: the device preview frame

`DevicePreviewControl` is a model of the physical device, not an ordinary card.
Page design must not change it at will.

### 4.1 Fixed parameters

- Control outer size: 154 × 440.
- Visible screen: 142 × 428. The ratio and pixel mapping cannot change.
- Silver frame width: exactly 6 px on all four sides.
- Silver frame colour: `#D3D7DC`.
- Outer radius 26, inner radius 20. They are concentric and the difference
  equals the frame width exactly.
- The screen takes no extra black border, shadow, stroke or second clip.
- Firmware occlusions: the round one on the left and the capsule on the right
  are positioned and scaled in 142 × 428 coordinates, one flush left, one flush
  right.
- Bitmap scaling uses HighQuality. Page scaling must never produce a
  non-integer frame width.

### 4.2 What may change

- The 142 × 428 screen bitmap.
- The icons inside the firmware occlusions (once real device assets exist).
- The description text on the preview card.

### 4.3 Bar for changing it

Any change to the outer size, frame width, radii, screen ratio or occlusion
coordinates must:

1. be checked against photographs of the real device;
2. be screenshotted at 100% and 125% Windows scaling;
3. be verified for equal width on all four sides, continuous corners, and
   concentric inner and outer radii;
4. update this document's version number and locked parameters.

### 4.4 The AI usage theme

- The theme id is fixed as `ai-quota`, registered as its own `IScreenTheme`.
  It does not modify other themes.
- One platform is rendered at a time. The platform name sits below the energy
  bar; there is no platform list or switcher.
- The main visual is a centred vertical rounded energy bar that fills from the
  bottom up, its height matching the remaining quota percentage exactly.
- The line below the platform name shows the remaining quota: for a
  subscription with a remaining count, `56% / 1 left`; otherwise just `56%`.
- The 52 px firmware safe area still applies at the top, and helper text must
  not overlap the round indicator on the left or the capsule on the right.
- The data model covers API-key tokens, cost, quota or request counts, as well
  as subscription percentage, remaining count, reset period and reset time.
- The theme only reads a generic snapshot and draws it. Platform
  authentication, API requests and key storage belong to a separate data-source
  adapter.

## 5. What may and may not change

### The product lets users change

- The keyboard screen's accent colour.
- The keyboard screen's font.
- The application language.
- The theme, the image, the refresh interval, the automation switches, the
  device address and the content safe area.

### Development may iterate, but through tokens

- Page information architecture, explanatory text, card composition, entry
  points.
- Semantic colour and size tokens — these need a global regression and must
  never be overridden in a single page.

### Do not change casually

- The locked device preview parameters.
- The 142 × 428 and 512 KB JPEG interface constraints.
- The single-UI-font rule on the control side.
- The template and state system shared by controls of the same kind.
- The auto-save and auto-push pipeline that follows a settings change.

## 6. Development constraints

- New pages may reference design tokens and named styles only.
- Apart from image content, status colours and the locked device component, no
  new HEX colour may appear in page XAML.
- Do not create new Button, TextBox, ComboBox, CheckBox, Slider or ScrollBar
  templates in page XAML.
- When a new component is needed, define it in `DesignSystem.axaml` first, then
  reference it from the page.
- The device rendering font and the control-side UI font must travel through
  different data channels.

## 7. Delivery checklist

- [ ] No clipping, misalignment or aliasing at 100%, 125% and 150% scaling.
- [ ] The 1080 × 680 minimum window and the maximised layout both work.
- [ ] Mouse Hover/Pressed, keyboard Focus and Disabled states are consistent.
- [ ] No grey system ComboBox, no system-arrow scrollbar, no dotted focus box.
- [ ] Apart from the live font preview, the control side inherits the Windows
      system font stack, and the release contains no PingFang.ttc or any other
      font without redistribution rights.
- [ ] In the 36 px, 40 px and 44 px inputs, "北京 / Ag09" is fully and
      vertically centred when unfocused, hovered and focused; every input has
      zero vertical padding.
- [ ] A fixed-length input still shows its full value with the caret at the
      end; all seven characters of a HEX colour are visible.
- [ ] The silver device frame is 6 px on all four sides, its corners are
      continuous, and the inner and outer radii are concentric.
- [ ] The keyboard preview and the actual JPEG come from the same frame.
- [ ] Importing a picture theme pushes once and does not start a scheduled push.
- [ ] Switching theme and any settings change saves and pushes automatically;
      a continuous interaction commits only the final value.
- [ ] Minimise, close-to-tray, tray double-click restore, tray exit and
      start-minimised all behave correctly.
- [ ] Built-in theme ids are unique, and every theme renders as a 142 × 428
      baseline JPEG under 512 KB.
- [ ] The portable build still uses `Fonts` and `Data` beside the executable.
- [ ] Every language renders every theme within the size budget, and no label
      in a narrow column is truncated.

## 8. Desktop motion

This section applies to the .NET desktop control-side interface only. It must
not reach `KeyboardScreen.Core`, the 142 × 428 rendering, the device preview
pixels, or the JPEG output pipeline.

- Basis: the continuity, direct manipulation, clear feedback and restraint
  principles of the Apple Human Interface Guidelines — without copying the
  platform's appearance.
- Allowed properties: `Opacity`, `RenderTransform.TranslateTransform` and
  `RenderTransform.ScaleTransform`, by default nothing else.
- Forbidden properties: never animate `Width`, `Height`, `Margin`, grid column
  widths, padding, or anything else that triggers a layout pass.
- Durations: press 90 ms, hover 140 ms, release and settle 180 ms, page or
  dialog enter 220 ms, exit 130 ms.
- Curves: enter and release use a natural `CubicEase/EaseOut`; press and exit
  use `CubicEase/EaseInOut`. No spring, bounce, elastic, floating, breathing or
  looping animation.
- Continuity: code-driven animation uses `HandoffBehavior.SnapshotAndReplace`
  and continues from the element's current visual value. Storyboards are never
  queued or stacked.
- Direct manipulation: sliders, the colour picker pointer and scrolling content
  follow the input directly, with no lag animation on the drag position.
- Performance: no large-area Blur or DropShadow and no per-element animation in
  long lists for the sake of motion. WebViews, the device preview bitmap and
  large images are never animated continuously.
- Accessibility: when Windows client animations are turned off, the interface
  jumps straight to its final state.
- Covered interactions: buttons, top-level navigation, the theme list,
  drop-downs, switches, page transitions, contextual cards, modal dialogs,
  minimise and restore.
- Interaction motion uses XAML `Transitions` and the behaviours under
  `src/KeyboardScreen.App.Avalonia/Behaviors/` (EdgeFadeBehavior,
  SmoothScrollBehavior). Page code must not duplicate duration, easing or
  animation-management logic.
- New surfaces: reuse the global control styles first. Call
  `InteractionMotion.Reveal` or `HideAsync` only for a page or dialog
  transition with clear semantics.

## 9. Localization

The application ships English, Ukrainian and Simplified Chinese. Everything a
user can read — the desktop UI, the themes rendered onto the keyboard, service
and error messages — comes from the catalogue, never from a literal in code.

- Catalogues are embedded JSON, one file per language, under
  `src/KeyboardScreen.Core/Localization/`. They are embedded rather than
  shipped as satellite assemblies so the single-file portable build keeps
  working.
- Keys are PascalCase identifiers with no dots or spaces: the XAML binding
  resolves them through an indexer, so a key must be a valid path segment.
- In XAML use `Text="{infra:Localize Key}"`. In code use `Loc.T("Key")`, or
  `Loc.T("Key", arg0, arg1)` when the string takes placeholders.
- Dates, weekday names and numbers are formatted with `Loc.Culture`, or through
  the `Loc.LongDate`, `Loc.ShortDate` and `Loc.DayName` helpers. Never rely on
  the ambient culture in rendering code.
- A missing key falls back to English and then to the key itself, so a gap
  shows readable text rather than an empty label.
- Text stored in a field, rather than read through a property, must be
  refreshed when the language changes. The view models do this from
  `Loc.Instance.LanguageChanged`.
- Anything a user chooses from a list is stored as a value, never as its
  display text — otherwise changing language invalidates the saved setting.

### 9.1 Writing for the 142 px screen

Keys prefixed `Screen*` are drawn onto the keyboard display, where a label
column is about 49 px wide and the full safe width is 122 px. The renderer
truncates with an ellipsis; it does not wrap.

- Keep an English or Ukrainian screen label no longer than the Chinese original
  looks. Where the full word does not fit, define a separate short key
  (`ScreenLabelMemoryShort`, `ScreenPeriodQuarter`) rather than shortening the
  one used in wider layouts.
- The bundled MiSans covers Latin, Cyrillic and CJK. Doto, used by the
  dot-matrix themes, covers digits and Latin only — those themes must therefore
  keep passing digits to Doto and let labels fall to the user-selected font.

### 9.2 Adding a language

1. Add an entry to `AppLanguageInfo.All` with its id, native name and culture.
2. Add `Strings.<id>.json` with the same keys as `Strings.en.json`.
3. Register the file as an `EmbeddedResource` in `KeyboardScreen.Core.csproj`.
4. If a third-party service needs a language code, add it to the catalogue
   (`GeocodingLanguageCode`, `ReverseGeocodeLanguageCode`) rather than mapping
   it in code.

The smoke tests fail the build when the catalogues drift apart, when a value is
empty, when a translation does not take the same `{0}`…`{n}` placeholders as
English, or when a theme fails to render in any language.
