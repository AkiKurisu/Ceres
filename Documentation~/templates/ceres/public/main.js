// Ceres customization for the DocFX "modern" template.
// See https://dotnet.github.io/docfx/docs/template.html#extend-via-javascript
export default {
  // Dark-first brand experience (user can still toggle in the navbar).
  defaultTheme: 'dark',

  // Click images to open them in a lightbox.
  showLightbox: () => true,

  // Add a GitHub link to the navbar.
  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/AkiKurisu/Ceres',
      title: 'GitHub'
    }
  ]
}
