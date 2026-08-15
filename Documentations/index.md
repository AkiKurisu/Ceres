---
_disableToc: true
_disableContribution: true
_disableAffix: true
---

<div class="ceres-home">
  <section class="ceres-hero">
    <div class="ceres-hero-field" aria-hidden="true"><span></span><span></span><span></span></div>
    <div class="ceres-hero-inner">
      <div class="ceres-hero-copy ceres-reveal">
        <p class="ceres-eyebrow">C#-first Unity framework</p>
        <h1>Build gameplay.<br><em>Extend behavior visually.</em></h1>
        <div class="ceres-actions">
          <a class="ceres-button ceres-button-primary" href="docs/getting_started.md">Get started</a>
          <a class="ceres-button ceres-button-secondary" href="https://github.com/AkiKurisu/Ceres" target="_blank" rel="noopener">View on GitHub</a>
        </div>
        <div class="ceres-install" aria-label="Install from Git URL">
          <span class="ceres-install-label">Unity Package Manager</span>
          <div class="ceres-install-command" data-command>
            <code>https://github.com/AkiKurisu/Ceres.git?path=Packages/com.kurisu.ceres</code>
            <button type="button" data-copy aria-label="Copy Git package URL">
              <span class="ceres-copy-icon ceres-copy-icon-copy" aria-hidden="true"><svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect width="14" height="14" x="8" y="8" rx="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/></svg></span>
              <span class="ceres-copy-icon ceres-copy-icon-check" aria-hidden="true"><svg viewBox="0 0 24 24" width="15" height="15" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 6 9 17l-5-5"/></svg></span>
              <span class="ceres-copy-label visually-hidden">Copy</span>
            </button>
          </div>
        </div>
      </div>
      <figure class="ceres-hero-media ceres-reveal">
        <img src="resources/images/ceres_banner.png" alt="Ceres visual scripting editor" />
      </figure>
    </div>
  </section>

  <section class="ceres-stories">
    <article class="ceres-story">
      <div class="ceres-story-inner ceres-reveal">
        <div class="ceres-story-copy">
          <p class="ceres-eyebrow">From iteration to runtime</p>
          <h2>Visual logic without a throwaway execution path.</h2>
          <p>Debug and hot reload Flow graphs while iterating. Generate typed C# runtime programs when the same graph needs production execution.</p>
          <div class="ceres-story-links"><a href="docs/flow_startup.md">Explore Flow</a><a href="docs/flow_codegen.md">Code generation</a></div>
        </div>
        <figure class="ceres-story-media"><img src="resources/images/ceres_flow.png" alt="Ceres Flow graph editor" loading="lazy" /></figure>
      </div>
    </article>
    <article class="ceres-story ceres-story-flip">
      <div class="ceres-story-inner ceres-reveal">
        <div class="ceres-story-copy">
          <p class="ceres-eyebrow">Gameplay presentation</p>
          <h2>Drive animation and presentation from code or Flow.</h2>
          <p>A script-driven PlayableGraph runtime sits alongside reactive graphics settings, pooled audio and effects, and data-driven level orchestration.</p>
          <div class="ceres-story-links"><a href="docs/gameplay_animation.md">Animation runtime</a><a href="xref:Ceres.Gameplay.Animations">Animation API</a></div>
        </div>
        <figure class="ceres-story-media"><img src="resources/images/gameplay-animation-playablegraph.svg" alt="Ceres PlayableGraph animation runtime" loading="lazy" /></figure>
      </div>
    </article>
  </section>

  <section class="ceres-pillars" aria-labelledby="ceres-pillars-title">
    <div class="ceres-section-heading ceres-reveal">
      <p class="ceres-eyebrow">Why Ceres</p>
      <h2 id="ceres-pillars-title">A focused stack for gameplay programmers.</h2>
      <p>Use the parts you need. Keep your C# architecture in control.</p>
    </div>
    <div class="ceres-pillar-grid">
      <a class="ceres-pillar ceres-reveal" href="docs/flow_concept.md"><span>01</span><h3>C#-first visual scripting</h3><p>Expose typed APIs to Flow, iterate with live graphs, then generate C# for performance-sensitive and IL2CPP builds.</p></a>
      <a class="ceres-pillar ceres-reveal" href="docs/gameplay.md"><span>02</span><h3>Actor-based world design</h3><p>Build on a lightweight scaffold of Actors, Components, controllers, and lifecycle-managed world services.</p></a>
      <a class="ceres-pillar ceres-reveal" href="docs/core_data_driven.md"><span>03</span><h3>Data-driven gameplay</h3><p>Author typed Data Tables and hierarchical configuration, then tune live parameters through console variables.</p></a>
      <a class="ceres-pillar ceres-reveal" href="docs/core_content_pipeline.md"><span>04</span><h3>Graph-driven content pipeline</h3><p>Create deterministic Addressables builds, validated updates, and relocatable runtime content packages.</p></a>
      <a class="ceres-pillar ceres-reveal" href="docs/gameplay.md"><span>05</span><h3>PlayableGraph animation</h3><p>Compose clips and Animator Controllers with cross-fades, layers, masks, sequences, and notifications.</p></a>
      <a class="ceres-pillar ceres-reveal" href="docs/ceres_concept.md"><span>06</span><h3>Low-overhead foundations</h3><p>Use pooled lifetimes, sparse storage, reactive state, and allocation-free scheduling paths in hot gameplay code.</p></a>
    </div>
  </section>

  <section class="ceres-final">
    <div class="ceres-final-inner ceres-reveal">
      <p class="ceres-eyebrow">Start with the architecture you need</p>
      <h2>Bring Ceres into your Unity project.</h2>
      <div class="ceres-actions ceres-actions-centered">
        <a class="ceres-button ceres-button-primary" href="docs/getting_started.md">Read the documentation</a>
        <a class="ceres-button ceres-button-secondary" href="xref:Ceres">Browse the API</a>
      </div>
    </div>
  </section>
</div>
