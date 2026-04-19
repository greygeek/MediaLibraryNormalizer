# Avalonia UI Theme Guide (Light + Dark)

## Overview

This document defines a modern, gradient-based theme system for Avalonia
UI.

------------------------------------------------------------------------

## Base Theme (Shared)

### Typography

-   Font: Inter, Segoe UI

### Spacing

-   XS: 4px
-   SM: 8px
-   MD: 16px
-   LG: 24px

### Corner Radius

-   Small: 6px
-   Medium: 10px
-   Large: 14px

------------------------------------------------------------------------

## Light Theme

### Colors

-   Background: #F5F7FB
-   Surface: #FFFFFF
-   Border: #E2E8F0
-   Text Primary: #1E293B
-   Text Secondary: #64748B

### Accents

-   Primary: #6366F1
-   Secondary: #8B5CF6
-   Cyan: #06B6D4

### Status

-   Success: #22C55E
-   Warning: #F59E0B
-   Error: #EF4444

------------------------------------------------------------------------

## Dark Theme

### Colors

-   Background: #0B1120
-   Surface: #111827
-   Border: #2D3748
-   Text Primary: #E5E7EB
-   Text Secondary: #9CA3AF

### Accents

-   Primary: #818CF8
-   Secondary: #A78BFA
-   Cyan: #22D3EE

### Status

-   Success: #4ADE80
-   Warning: #FBBF24
-   Error: #F87171

------------------------------------------------------------------------

## Key UI Patterns

### Cards

-   Rounded corners (14px)
-   Subtle border + shadow

### Buttons

-   Primary: gradient background
-   Secondary: soft fill
-   Danger: red emphasis

### Status Pills

-   Rounded (pill shape)
-   Color-coded by status

------------------------------------------------------------------------

## Implementation Notes

-   Use ResourceDictionaries
-   Use DynamicResource for theme switching
-   Separate Light.axaml and Dark.axaml
-   Add animations (hover, selection, transitions)
-   Build a reusable component library (SeriesCard, StatPill, etc.)
