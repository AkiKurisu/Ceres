function initializeCeresTheme() {
  const home = document.querySelector('.ceres-home')
  document.body.classList.toggle('ceres-home-page', Boolean(home))

  const pageRoot = document.documentElement
  pageRoot.classList.toggle('ceres-home-active', Boolean(home))
  if (home && pageRoot.dataset.ceresNavReady !== 'true') {
    pageRoot.dataset.ceresNavReady = 'true'
    let solid = window.scrollY >= 24
    let scheduledFrame = 0
    const applyNavState = () => pageRoot.classList.toggle('ceres-home-nav-solid', solid)
    const updateNavState = () => {
      scheduledFrame = 0
      if (!solid && window.scrollY >= 24) {
        solid = true
        applyNavState()
      } else if (solid && window.scrollY <= 8) {
        solid = false
        applyNavState()
      }
    }
    const scheduleNavUpdate = () => {
      if (scheduledFrame) return
      scheduledFrame = window.requestAnimationFrame(updateNavState)
    }
    applyNavState()
    window.addEventListener('scroll', scheduleNavUpdate, { passive: true })
  }

  // DocFX resolves xref anchors after stripping author-provided classes.
  document.querySelectorAll('.ceres-actions-centered a.xref').forEach((link) => {
    link.classList.add('ceres-button', 'ceres-button-secondary')
  })

  const revealItems = document.querySelectorAll('.ceres-reveal:not(.is-visible)')
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (!entry.isIntersecting) continue
        entry.target.classList.add('is-visible')
        observer.unobserve(entry.target)
      }
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 })
    revealItems.forEach((item) => observer.observe(item))
  } else {
    revealItems.forEach((item) => item.classList.add('is-visible'))
  }

  document.querySelectorAll('[data-copy]').forEach((button) => {
    if (button.dataset.copyReady === 'true') return
    button.dataset.copyReady = 'true'
    button.addEventListener('click', async () => {
      const command = button.closest('[data-command]')?.querySelector('code')?.textContent?.trim()
      if (!command) return
      await navigator.clipboard.writeText(command)
      const label = button.querySelector('.ceres-copy-label')
      button.classList.add('is-copied')
      button.setAttribute('aria-label', 'Copied Git package URL')
      if (label) label.textContent = 'Copied'
      window.setTimeout(() => {
        button.classList.remove('is-copied')
        button.setAttribute('aria-label', 'Copy Git package URL')
        if (label) label.textContent = 'Copy'
      }, 1400)
    })
  })
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeCeresTheme, { once: true })
} else {
  initializeCeresTheme()
}

export default {
  defaultTheme: 'auto',
  showLightbox: () => true,
  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/AkiKurisu/Ceres',
      title: 'GitHub'
    }
  ]
}
