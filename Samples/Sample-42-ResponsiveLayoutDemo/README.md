# Sample-42-ResponsiveLayoutDemo

This sample demonstrates how to implement a responsive UI that adapts to different screen sizes (Mobile, Tablet, Desktop) using Guinevere.

## Responsive Thresholds
- **Mobile**: < 600px width. Single column layout with bottom navigation.
- **Tablet**: 600px - 900px width. Two-column layout with side-by-side content and sidebar.
- **Desktop**: > 900px width. Three-column layout with header navigation, main content, and two sidebars.

## Features
- Dynamic layout switching based on `gui.ScreenRect.W`.
- Adaptive margins, gaps, and component visibility.
- Different navigation patterns (Bottom vs Header) based on form factor.
