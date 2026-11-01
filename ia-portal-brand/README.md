# IA-Portal brand assets

Vector interpretation of the approved blue/cyan/violet geometric IA-Portal concept. This is a usable logo asset, not a pixel-perfect tracing of the earlier generated reference poster.

- `ia-portal-logo.svg`: full wordmark on light surfaces.
- `ia-portal-logo-dark.svg`: full wordmark on dark surfaces.
- `ia-portal-mark.svg`: symbol only, for compact navigation and favicon.

Suggested location in the React project: `front/public/brand/`.

Example:
```jsx
<img src={isDark ? '/brand/ia-portal-logo-dark.svg' : '/brand/ia-portal-logo.svg'} alt="IA-Portal" width={190} />
```

Avoid changing these SVGs with external rasterization unless necessary. Fonts in the wordmark use system fallbacks.
