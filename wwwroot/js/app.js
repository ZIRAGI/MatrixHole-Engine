(() => {
  // ============================================================
  // UTILITIES
  // ============================================================
  const $ = (q, el = document) => el.querySelector(q);
  const $$ = (q, el = document) => Array.from(el.querySelectorAll(q));

  const debounce = (fn, ms = 150) => {
    let t;
    return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), ms); };
  };

  const safeJson = (raw, fallback = null) => {
    try { return JSON.parse(raw); } catch { return fallback; }
  };

  const safeApi = (fn, fallback = null) => {
    try { return fn(); } catch (e) { console.warn('API error:', e); return fallback; }
  };

  // ============================================================
  // SPLASH SCREEN
  // ============================================================
  function hideSplashScreen() {
    const splash = $('#splashScreen');
    if (splash) {
      splash.classList.add('hidden');
      setTimeout(() => splash.remove(), 700);
    }
  }
  window.addEventListener('load', () => {
    setTimeout(hideSplashScreen, 1800);
  });

  // ============================================================
  // API BRIDGE
  // ============================================================
  let api = null;
  try { api = window.chrome?.webview?.hostObjects?.sync?.csApi; } catch (e) {}

  // ============================================================
  // STATE
  // ============================================================
  let dirty = false;
  let rknBypassActive = false;

  // ============================================================
  // STATUS BAR
  // ============================================================
  const $statusLeft = $('#statusLeft');
  const status = (msg, type = 'info') => {
    if ($statusLeft) {
      if (window.I18N?.loaded) {
        const key = msg.toLowerCase().replace(/\s+/g, '_');
        const translated = window.I18N.t(key);
        $statusLeft.textContent = translated !== key ? translated : msg;
      } else {
        $statusLeft.textContent = msg;
      }
    }
    showToast(msg, type);
  };

  function showToast(message, type = 'info', duration = 3500) {
    const container = $('#toastContainer');
    if (!container) return;
    const icons = {
      success: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>',
      error: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>',
      warning: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>',
      info: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>'
    };
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
      <div class="toast-icon">${icons[type] || icons.info}</div>
      <div class="toast-content">
        <div class="toast-message">${message.replace(/</g, '&lt;')}</div>
      </div>
      <button class="toast-close" onclick="this.parentElement.classList.add('toast-out');setTimeout(()=>this.parentElement.remove(),300)">
        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
      </button>
    `;
    container.appendChild(toast);
    setTimeout(() => {
      toast.classList.add('toast-out');
      setTimeout(() => toast.remove(), 300);
    }, duration);
  }

  const markDirty = debounce(() => {
    dirty = true;
    status('Unsaved changes');
  }, 80);

  // ============================================================
  // SMOOTH SCROLL
  // ============================================================
  (function initSmoothScroll() {
    const container = $('.content');
    if (!container) return;
    let targetY = container.scrollTop;
    let currentY = container.scrollTop;
    let raf = null;
    const ease = () => {
      const diff = targetY - currentY;
      if (Math.abs(diff) < 0.5) {
        currentY = targetY;
        container.scrollTop = currentY;
        raf = null;
        return;
      }
      currentY += diff * 0.12;
      container.scrollTop = currentY;
      raf = requestAnimationFrame(ease);
    };
    container.addEventListener('wheel', (e) => {
      e.preventDefault();
      targetY += e.deltaY;
      targetY = Math.max(0, Math.min(targetY, container.scrollHeight - container.clientHeight));
      if (!raf) raf = requestAnimationFrame(ease);
    }, { passive: false });
  })();

  // ============================================================
  // TABS
  // ============================================================
  const navBtns = $$('.nav-btn');
  const tabPanels = $$('.tab-panel');
  navBtns.forEach(btn => {
    btn.addEventListener('click', () => {
      const targetTab = btn.dataset.tab;
      const targetPanel = $(`#tab-${targetTab}`);
      if (!targetPanel || !targetPanel.classList.contains('hidden')) return;

      navBtns.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      // Animate out current panel
      const currentPanel = tabPanels.find(p => !p.classList.contains('hidden'));
      if (currentPanel) {
        currentPanel.style.transition = 'opacity 0.15s ease, transform 0.15s ease';
        currentPanel.style.opacity = '0';
        currentPanel.style.transform = 'translateX(-8px)';
        setTimeout(() => {
          currentPanel.classList.add('hidden');
          currentPanel.style.opacity = '';
          currentPanel.style.transform = '';
          currentPanel.style.transition = '';

          targetPanel.classList.remove('hidden');
          targetPanel.style.opacity = '0';
          targetPanel.style.transform = 'translateX(8px)';
          requestAnimationFrame(() => {
            targetPanel.style.transition = 'opacity 0.25s ease, transform 0.25s ease';
            targetPanel.style.opacity = '1';
            targetPanel.style.transform = 'translateX(0)';
            setTimeout(() => {
              targetPanel.style.transition = '';
              targetPanel.style.transform = '';
            }, 250);
          });
        }, 150);
      } else {
        targetPanel.classList.remove('hidden');
      }
    });
  });

  // ============================================================
  // TOGGLES
  // ============================================================
  function initToggles() {
    $$('.toggle-switch[data-key], .toggle-switch[data-launch], .toggle-switch[id^="stealth"]').forEach(sw => {
      sw.addEventListener('click', () => {
        sw.classList.toggle('on');
        if (sw.dataset.launch) updateLaunchPreview();
        else markDirty();
      });
    });
  }

  // ============================================================
  // SLIDERS (with debounced dirty marking)
  // ============================================================
  function initSliders() {
    $$('.slider-row').forEach(row => {
      const track = $('.slider-track', row);
      const fill = $('.slider-fill', row);
      const thumb = $('.slider-thumb', row);
      const valEl = $('.slider-value', row);
      const min = +row.dataset.min, max = +row.dataset.max, step = +row.dataset.step;

      const setVisuals = (v) => {
        const pct = max === min ? 0 : ((v - min) / (max - min)) * 100;
        fill.style.width = pct + '%';
        thumb.style.left = pct + '%';
        valEl.textContent = v;
      };

      const setValue = (v, emit = true) => {
        v = Math.max(min, Math.min(max, v));
        v = Math.round((v - min) / step) * step + min;
        row._value = v;
        setVisuals(v);
        if (emit) markDirty();
      };

      const fromClientX = (cx) => {
        const rect = track.getBoundingClientRect();
        const pct = Math.max(0, Math.min(1, (cx - rect.left) / rect.width));
        return min + pct * (max - min);
      };

      let dragging = false;
      const onDown = (cx) => { dragging = true; setValue(fromClientX(cx)); };
      const onMove = (cx) => { if (dragging) setValue(fromClientX(cx)); };
      const onUp = () => { dragging = false; };

      track.addEventListener('mousedown', e => onDown(e.clientX));
      window.addEventListener('mousemove', e => onMove(e.clientX));
      window.addEventListener('mouseup', onUp);
      track.addEventListener('touchstart', e => { onDown(e.touches[0].clientX); }, { passive: false });
      window.addEventListener('touchmove', e => { if (dragging) { e.preventDefault(); onMove(e.touches[0].clientX); } }, { passive: false });
      window.addEventListener('touchend', onUp);

      row._setValue = setValue;
    });
  }

  // ============================================================
  // PRESETS
  // ============================================================
  $$('.preset-btn').forEach(btn => {
    btn.addEventListener('click', async () => {
      $$('.preset-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      if (!api) return;
      const res = safeApi(() => safeJson(api.ApplyPreset(btn.dataset.preset)));
      if (res) {
        applyConfigToUI(res);
        const label = btn.querySelector('span')?.textContent || btn.dataset.preset;
        status(`Applied preset: ${label}`);
        dirty = false;
      } else {
        status('Error applying preset');
      }
    });
  });

  // ============================================================
  // CONFIG SYNC
  // ============================================================
  function applyConfigToUI(cfg) {
    if (!cfg) return;
    $$('.toggle-switch[data-key]').forEach(sw => {
      const key = sw.dataset.key;
      let val = cfg[key];
      if (typeof val === 'number') val = val !== 0;
      sw.classList.toggle('on', !!val);
    });
    $$('.slider-row[data-key]').forEach(row => {
      const key = row.dataset.key;
      let val = cfg[key];
      if (val === undefined || val === null) return;
      if (key === 'fps_limit' && val === -1) val = 0;
      if (key === 'particle_raycast_budget') {
        const map = {16:0, 64:1, 256:2, 1024:3, 4096:4};
        val = map[val] ?? val;
      }
      if (key === 'lod_bias') val = Math.round(val * 10);
      if (key === 'anti_aliasing') {
        const valid = [0, 2, 4, 8];
        val = valid.reduce((prev, curr) => Math.abs(curr - val) < Math.abs(prev - val) ? curr : prev);
      }
      if (row._setValue) row._setValue(val, false);
    });
  }

  function gatherConfigFromUI() {
    const cfg = {};
    $$('.toggle-switch[data-key]').forEach(sw => {
      const key = sw.dataset.key;
      const on = sw.classList.contains('on');
      if (key === 'anisotropic_filtering' || key === 'soft_particles' || key === 'vsync') {
        cfg[key] = on ? 1 : 0;
      } else {
        cfg[key] = on;
      }
    });
    $$('.slider-row[data-key]').forEach(row => {
      let val = row._value ?? +row.dataset.min;
      const key = row.dataset.key;
      if (key === 'fps_limit') val = val === 0 ? -1 : val;
      if (key === 'particle_raycast_budget') {
        const map = [16, 64, 256, 1024, 4096];
        val = map[val] ?? 256;
      }
      if (key === 'lod_bias') val = val / 10;
      if (key === 'anti_aliasing') {
        const valid = [0, 2, 4, 8];
        val = valid.reduce((prev, curr) => Math.abs(curr - val) < Math.abs(prev - val) ? curr : prev);
      }
      cfg[key] = val;
    });
    // Force integers for registry values
    ['graphics_quality','draw_distance','camera_fog','fps_limit','texture_quality','anti_aliasing',
     'shadow_distance','shadow_cascades','ragdoll_limit','master_volume','sfx_volume','voice_volume',
     'music_volume','voice_noise_suppression','max_ping','particle_raycast_budget',
     'anisotropic_filtering','soft_particles'].forEach(k => {
      if (k in cfg) cfg[k] = Math.round(cfg[k]);
    });
    return cfg;
  }

  async function saveConfig() {
    if (!api) return;
    const cfg = gatherConfigFromUI();
    const res = safeApi(() => safeJson(api.SaveConfig(JSON.stringify(cfg)), {ok:false}));
    if (res?.ok) { status('Config saved'); dirty = false; }
    else status('Save failed: ' + (res?.error || ''));
  }

  // ============================================================
  // LAUNCH OPTIONS
  // ============================================================
  const $launchPreview = $('#launchOptionsPreview');
  const $customLaunchArgs = $('#customLaunchArgs');
  function updateHomeLaunchPreview() {
    const parts = [];
    if ($('.toggle-switch[data-launch="dx11"]')?.classList.contains('on')) parts.push('-force-d3d11');
    if ($('.toggle-switch[data-launch="vulkan"]')?.classList.contains('on')) parts.push('-force-vulkan');
    if ($('.toggle-switch[data-launch="opengl"]')?.classList.contains('on')) parts.push('-force-opengl');
    if ($('.toggle-switch[data-launch="fastest"]')?.classList.contains('on')) parts.push('-screen-quality Fastest');
    if ($('.toggle-switch[data-launch="borderless"]')?.classList.contains('on')) parts.push('-screen-fullscreen 0 -popupwindow');
    if ($('.toggle-switch[data-launch="nogpu"]')?.classList.contains('on')) parts.push('-disable-gpu-skinning');
    if ($('.toggle-switch[data-launch="nolog"]')?.classList.contains('on')) parts.push('-nolog');
    if ($('.toggle-switch[data-launch="nojoy"]')?.classList.contains('on')) parts.push('-nojoy');
    if ($('.toggle-switch[data-launch="high"]')?.classList.contains('on')) parts.push('-high');
    if ($('.toggle-switch[data-launch="malloc"]')?.classList.contains('on')) parts.push('-malloc=system');
    if ($('.toggle-switch[data-launch="allcores"]')?.classList.contains('on')) parts.push('-USEALLAVAILABLECORES');
    if ($customLaunchArgs?.value.trim()) parts.push($customLaunchArgs.value.trim());
    if ($launchPreview) $launchPreview.textContent = parts.join(' ') || '(none)';
    return parts.join(' ');
  }

  $customLaunchArgs?.addEventListener('input', updateHomeLaunchPreview);

  $('#saveLaunchBtn')?.addEventListener('click', () => {
    if (!api) return;
    const opts = updateHomeLaunchPreview();
    const res = safeApi(() => safeJson(api.SetLaunchOptions(opts)), {ok:false});
    status(res?.ok ? 'Launch options saved' : 'Failed to save');
  });
  $('#saveLaunchBtnHome')?.addEventListener('click', () => {
    if (!api) return;
    const opts = updateHomeLaunchPreview();
    const res = safeApi(() => safeJson(api.SetLaunchOptions(opts)), {ok:false});
    status(res?.ok ? 'Launch options saved' : 'Failed to save');
  });

  $('#launchGameBtn')?.addEventListener('click', () => {
    if (api) safeApi(() => api.LaunchGame());
    status('Launching game...');
  });
  $('#launchGameBtnHome')?.addEventListener('click', () => {
    if (api) safeApi(() => api.LaunchGame());
    status('Launching game...');
  });

  // ============================================================
  // CHEATS — LAYER 1: FILE NUKES
  // ============================================================
  function setNukeStatus(id, text, isActive, isDanger) {
    const row = $(`#nuke${id}Row`);
    const st = $(`#nuke${id}Status`);
    if (st) st.textContent = text;
    if (row) {
      row.classList.remove('active', 'danger-active');
      if (isActive) row.classList.add(isDanger ? 'danger-active' : 'active');
    }
  }

  const nukeHandlers = {
    Ac: {
      el: '#nukeAcToggle',
      enable: () => api?.EnableAcBypass(),
      disable: () => api?.DisableAcBypass(),
      activeText: 'AC BYPASSED',
      restoredText: 'AC RESTORED',
      statusActive: 'Anti-cheat bypassed',
      statusRestored: 'AC restored',
      statusError: 'AC operation failed'
    },
    Boot: {
      el: '#nukeBootToggle',
      enable: () => api?.ApplyBootConfigFps(),
      disable: () => api?.RestoreBootConfig(),
      formatActive: (r) => `NUKED (${r.tweaks_applied} tweaks)`,
      restoredText: 'RESTORED',
      statusActive: 'Boot.config nuked',
      statusRestored: 'Boot.config restored',
      statusError: 'Boot operation failed'
    },
    Ggm: {
      el: '#nukeGgmToggle',
      enable: () => api?.ApplyGlobalGameManagersFps(),
      disable: () => api?.RestoreGlobalGameManagers(),
      formatActive: (r) => `DESTROYED (${r.levels_modified} levels)`,
      restoredText: 'RESTORED',
      statusActive: 'GlobalGameManagers destroyed',
      statusRestored: 'GGM restored',
      statusError: 'GGM operation failed'
    },
    Crash: {
      el: '#nukeCrashToggle',
      enable: () => api?.DisableCrashHandler(),
      disable: () => api?.EnableCrashHandler(),
      activeText: 'DISABLED',
      restoredText: 'RESTORED',
      statusActive: 'Crash handler disabled',
      statusRestored: 'Crash handler restored',
      statusError: 'Crash handler operation failed'
    },
    Analytics: {
      el: '#nukeAnalyticsToggle',
      enable: () => api?.DisableAnalytics(),
      disable: () => api?.EnableAnalytics(),
      activeText: 'REMOVED',
      restoredText: 'RESTORED',
      statusActive: 'Analytics disabled',
      statusRestored: 'Analytics restored',
      statusError: 'Analytics operation failed'
    }
  };

  function ensureAcBypass() {
    const acSw = $('#nukeAcToggle');
    if (!acSw || acSw.classList.contains('on')) return;
    acSw.classList.add('on');
    if (!api) return;
    const res = safeApi(() => safeJson(api.EnableAcBypass()), {success:false});
    setNukeStatus('Ac', res.success ? 'AC BYPASSED' : `Error: ${res.error}`, res.success, false);
  }

  Object.entries(nukeHandlers).forEach(([id, h]) => {
    const sw = $(h.el);
    if (!sw) return;
    sw.addEventListener('click', () => {
      sw.classList.toggle('on');
      if (!api) return;
      const on = sw.classList.contains('on');
      // Auto-enable AC bypass for any file modification that isn't AC itself
      if (on && id !== 'Ac') ensureAcBypass();
      const raw = safeApi(() => on ? h.enable() : h.disable());
      const res = safeJson(raw, {success:false});
      const activeText = h.formatActive ? h.formatActive(res) : h.activeText;
      const text = res.success ? (on ? activeText : h.restoredText) : `Error: ${res.error}`;
      setNukeStatus(id, text, on, false);
      status(res.success ? (on ? h.statusActive : h.statusRestored) : h.statusError);
    });
  });

  // ============================================================
  // CHEATS — LAYER 1b: GAMEASSEMBLY HEX
  // ============================================================
  $('#btnAnalyzeGa')?.addEventListener('click', () => {
    if (!api) return;
    $('#gaStatus').textContent = 'Analyzing GameAssembly.dll...';
    const res = safeApi(() => safeJson(api.AnalyzeGameAssembly()), {});
    $('#gaStatus').textContent = res.error ? `Error: ${res.error}` : `Size: ${res.size} bytes | Strings: ${res.found_strings?.join(', ')}`;
    status('GameAssembly analyzed');
  });

  $('#btnApplyGa')?.addEventListener('click', () => {
    if (!api) return;
    $('#gaStatus').textContent = 'Hex-patching GameAssembly.dll...';
    const res = safeApi(() => safeJson(api.ApplyGameAssemblyPatches()), {success:false});
    $('#gaStatus').textContent = res.success ? `Patched: ${res.applied}, Skipped: ${res.skipped}, Failed: ${res.failed}` : `Error: ${res.error}`;
    status(res.success ? 'GameAssembly hex-patched' : 'Hex patch failed');
  });

  $('#btnRestoreGa')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.RestoreGameAssembly()), {success:false});
    $('#gaStatus').textContent = res.success ? `Restored ${res.restored}` : `Error: ${res.error}`;
    status(res.success ? 'GameAssembly restored' : 'Restore failed');
  });

  // ============================================================
  // CHEATS — LAYER 2: LAUNCH STEALTH
  // ============================================================
  $('#btnPrepareStealth')?.addEventListener('click', () => {
    if (!api) return;
    const ac = $('#stealthAcToggle')?.classList.contains('on') ?? false;
    const boot = $('#stealthBootToggle')?.classList.contains('on') ?? false;
    const ggm = $('#stealthGgmToggle')?.classList.contains('on') ?? false;
    $('#stealthStatus').textContent = 'Preparing...';
    const res = safeApi(() => safeJson(api.PrepareStealthEnvironment(ac, boot, ggm)), {});
    $('#stealthStatus').textContent = JSON.stringify(res);
    status('Stealth environment prepared');
  });

  $('#btnLaunchStealth')?.addEventListener('click', () => {
    if (!api) return;
    const selfDestruct = $('#stealthSelfDestructToggle')?.classList.contains('on') ?? false;
    $('#stealthStatus').textContent = selfDestruct ? 'Launching... Tweaker will self-destruct in 3s.' : 'Launching...';
    const res = safeApi(() => safeJson(api.LaunchGameStealth(selfDestruct)), {});
    $('#stealthStatus').textContent = JSON.stringify(res);
    status('Launch command sent');
  });

  $('#btnRestoreStealth')?.addEventListener('click', () => {
    if (!api) return;
    $('#stealthStatus').textContent = 'Restoring...';
    const res = safeApi(() => safeJson(api.RestoreStealthEnvironment()), {});
    $('#stealthStatus').textContent = JSON.stringify(res);
    status('Environment restored');
  });

  // ============================================================
  // CHEATS — LAYER 3: RUNTIME MEMORY
  // ============================================================
  const $memPatchList = $('#memoryPatchList');

  $('#btnLoadPatchDb')?.addEventListener('click', () => {
    if (!api || !$memPatchList) return;
    const res = safeApi(() => safeJson(api.GetMemoryPatchDatabase()), {patches:[]});
    $memPatchList.innerHTML = '';
    if (res.patches) {
      for (const p of res.patches) {
        const div = document.createElement('div');
        div.className = 'mem-patch-card' + (p.is_placeholder ? ' placeholder' : '');
        div.innerHTML = `
          <div class="mem-patch-name">${p.name} <span class="mem-patch-tag ${p.is_placeholder ? 'placeholder' : 'ready'}">${p.is_placeholder ? 'Placeholder' : 'Ready'}</span></div>
          <div class="mem-patch-desc">${p.description}</div>
          <button class="btn-action apply-patch-btn" style="width:100%;" data-id="${p.id}" ${p.is_placeholder ? 'disabled' : ''}>Apply Patch</button>
        `;
        $memPatchList.appendChild(div);
      }
      $$('.apply-patch-btn', $memPatchList).forEach(btn => {
        btn.addEventListener('click', () => {
          if (!api) return;
          const id = btn.dataset.id;
          $('#memStatus').textContent = `Applying ${id}...`;
          const r = safeApi(() => safeJson(api.ApplyMemoryPatchById(id)), {success:false});
          $('#memStatus').textContent = r.success ? `${id}: patched at 0x${r.address?.toString(16)}` : `${id}: ${r.message || r.error}`;
        });
      });
    }
    status('Patch DB loaded');
  });

  $('#btnApplyAllMem')?.addEventListener('click', () => {
    if (!api) return;
    $('#memStatus').textContent = 'Applying all ready patches...';
    const res = safeApi(() => safeJson(api.ApplyAllMemoryPatches()), {applied:0,skipped:0,failed:0});
    $('#memStatus').textContent = `Applied: ${res.applied}, Skipped: ${res.skipped}, Failed: ${res.failed}`;
    status('Memory patches applied');
  });

  // ============================================================
  // SYSTEM — ACTION GRID
  // ============================================================
  function formatApiResult(raw) {
    if (!raw) return 'Done';
    const r = safeJson(raw);
    if (!r) return raw;
    if (r.ok === true) {
      if (r.count !== undefined) return `Killed ${r.count} services`;
      if (r.applied !== undefined) return `Applied ${r.applied} patches`;
      return 'Applied successfully';
    }
    if (r.ok === false) return 'Failed: ' + (r.error || r.message || 'unknown');
    if (r.success === true) return 'Success';
    if (r.success === false) return 'Failed: ' + (r.error || r.message || 'unknown');
    return raw;
  }

  const actionMap = {
    highPriority: () => api?.SetProcessHighPriority(),
    realtime: () => api?.SetProcessRealtime(),
    affinity: () => api?.SetProcessAffinity(),
    clearRam: () => api?.ClearWorkingSet(),
    timer: () => api?.SetTimerRes(),
    killServices: () => api?.KillServices()
  };

  $$('.btn-action').forEach(btn => {
    btn.addEventListener('click', () => {
      if (!api) return;
      const action = btn.dataset.action;
      const card = btn.closest('.action-card');
      const feedback = card ? $('.action-feedback', card) : null;
      const raw = safeApi(() => actionMap[action]?.() ?? '{}');
      const msg = formatApiResult(raw);
      if (card) {
        card.classList.add('applied');
        setTimeout(() => card.classList.remove('applied'), 2500);
      }
      if (feedback) {
        feedback.textContent = msg;
        setTimeout(() => feedback.textContent = '', 2500);
      }
      status(msg);
    });
  });

  // ============================================================
  // SYSTEM — TOGGLES (bidirectional sync)
  // ============================================================
  function makeToggleHandler(elId, enableFn, disableFn, onMsg, offMsg, failMsg) {
    const sw = $(elId);
    if (!sw) return;
    sw.addEventListener('click', () => {
      sw.classList.toggle('on');
      const on = sw.classList.contains('on');
      if (!api) return;
      const res = safeApi(() => safeJson(on ? enableFn() : disableFn()), {ok:false});
      status(res.ok ? (on ? onMsg : offMsg) : (failMsg || 'Failed'));
    });
  }

  makeToggleHandler('#togGameMode', () => api.EnableGameMode(), () => api.DisableGameMode(), 'Game Mode enabled', 'Game Mode disabled', 'Failed');
  makeToggleHandler('#togGameBar', () => api.DisableGameBar(), () => api.EnableGameBar(), 'Game Bar disabled', 'Game Bar enabled', 'Failed');
  makeToggleHandler('#togCoreParking', () => api.DisableCoreParking(), () => api.EnableCoreParking(), 'Core parking disabled', 'Core parking enabled', 'Failed');
  makeToggleHandler('#togHighPerf', () => api.SetHighPerfPlan(), () => api.SetBalancedPowerPlan(), 'High perf plan set', 'Balanced plan set', 'Failed');
  makeToggleHandler('#togFso', () => api.DisableFullscreenOpt(), () => api.EnableFullscreenOpt(), 'FSO disabled', 'FSO enabled', 'Failed');
  makeToggleHandler('#togHpet', () => api.DisableHPET(), () => api.EnableHPET(), 'HPET disabled', 'HPET enabled', 'Failed');
  makeToggleHandler('#togNagle', () => api.DisableNagle(), () => api.EnableNagle(), 'Nagle disabled', 'Nagle enabled', 'Failed');
  makeToggleHandler('#togSysmain', () => api.DisableSysMain(), () => api.EnableSysMain(), 'SysMain disabled', 'SysMain enabled', 'Failed');
  makeToggleHandler('#togVisFx', () => api.DisableVisualEffects(), () => api.EnableVisualEffects(), 'Visual effects disabled', 'Visual effects enabled', 'Failed');

  // ============================================================
  // NETWORK
  // ============================================================
  const $netStatusEl = $('#netStatus');
  const $netStatusDot = $netStatusEl ? $('.status-dot', $netStatusEl) : null;
  const $netStatusText = $netStatusEl ? $('.status-text', $netStatusEl) : null;

  function updateNetStatus() {
    if ($netStatusDot) {
      $netStatusDot.classList.toggle('on', rknBypassActive);
      $netStatusDot.classList.toggle('off', !rknBypassActive);
    }
    if ($netStatusText) {
      $netStatusText.textContent = rknBypassActive ? 'Bypass ACTIVE' : 'Bypass not active';
    }
    const hosts = $('#infoHosts'), dns = $('#infoDns'), proxy = $('#infoProxy');
    if (hosts) hosts.textContent = rknBypassActive ? 'ON' : 'OFF';
    if (dns) dns.textContent = rknBypassActive ? 'ON' : 'OFF';
    if (proxy) proxy.textContent = rknBypassActive ? 'ON' : 'OFF';
    $$('.info-bool').forEach(b => {
      b.classList.toggle('on', rknBypassActive);
      b.classList.toggle('off', !rknBypassActive);
    });
  }

  $('#btnRknApply')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.ApplyRknBypass()), {ok:false});
    if (res.ok) { rknBypassActive = true; updateNetStatus(); }
    status(res.ok ? `RKN bypass applied (${res.applied} steps)` : 'Bypass failed');
  });

  $('#btnRknReset')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.ResetRknBypass()), {ok:false});
    if (res.ok) { rknBypassActive = false; updateNetStatus(); }
    status(res.ok ? 'RKN bypass reset' : 'Reset failed');
  });

  $('#btnNetDiag')?.addEventListener('click', () => {
    if (!api) return;
    $('#netDiagOutput').textContent = 'Running diagnostics...';
    const text = safeApi(() => api.GetNetworkDiagnosticInfo(), 'Error');
    $('#netDiagOutput').textContent = text;
    status('Network diagnostics complete');
  });

  $('#btnAutoResetRkn')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.AutoResetRknIfNoInternet()), {reset:false});
    $('#netDiagOutput').textContent = res.message || 'Done';
    status(res.reset ? 'Auto-reset performed' : 'Internet OK');
    syncRknState();
  });

  $('#btnRknAnalyze')?.addEventListener('click', () => {
    if (!api) return;
    $('#rknAnalysisOutput').textContent = 'Analyzing...';
    const text = safeApi(() => api.AnalyzeRkn(), 'Error');
    $('#rknAnalysisOutput').textContent = text;
    status('RKN analysis complete');
  });

  $('#btnGenZapret')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.GenerateZapretConfig()), {ok:false});
    if (res.ok) {
      $('#rknAnalysisOutput').textContent = 'Zapret config generated:\n' + (res.config_path || '') + '\n' + (res.list_path || '');
      status('Zapret config generated');
    } else {
      $('#rknAnalysisOutput').textContent = 'Zapret not found or generation failed.';
      status('Zapret config failed');
    }
  });

  $('#btnApplyCustomHosts')?.addEventListener('click', () => {
    if (!api) return;
    const raw = $('#customHostsInput')?.value || '';
    const entries = raw.split(/\r?\n/).filter(l => l.trim());
    const res = safeApi(() => safeJson(api.ApplyCustomHosts(JSON.stringify(entries))), {ok:false});
    status(res.ok ? 'Custom hosts applied' : 'Custom hosts failed: ' + (res.error || ''));
    syncRknState();
  });

  $('#btnResetCustomHosts')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.ApplyCustomHosts(JSON.stringify([]))), {ok:false});
    status(res.ok ? 'Custom hosts reset' : 'Reset failed');
    syncRknState();
  });

  // Admin / Coder Network Toolkit
  const netToolkitOut = $('#netToolkitOutput');
  $('#btnNetInfo')?.addEventListener('click', () => {
    if (!api) return;
    netToolkitOut.textContent = 'Loading network info...';
    const text = safeApi(() => api.GetNetworkInfo(), 'Error');
    netToolkitOut.textContent = text;
  });
  $('#btnPingMonitor')?.addEventListener('click', () => {
    if (!api) return;
    netToolkitOut.textContent = 'Pinging...';
    const text = safeApi(() => api.PingMonitor(), 'Error');
    netToolkitOut.textContent = text;
  });
  $('#btnFlushDns')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.FlushDns()), {ok:false});
    status(res.ok ? 'DNS cache flushed' : 'Flush failed: ' + (res.error || ''));
  });
  $('#btnProcConns')?.addEventListener('click', () => {
    if (!api) return;
    netToolkitOut.textContent = 'Loading process connections...';
    const text = safeApi(() => api.GetProcessConnections(), 'Error');
    netToolkitOut.textContent = text;
  });
  $('#btnFirewallToggle')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.ToggleFirewall()), {ok:false});
    status(res.ok ? 'Firewall toggled: ' + (res.state || '') : 'Toggle failed: ' + (res.error || ''));
  });
  $('#btnAdapterOpt')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.OptimizeNetworkAdapter()), {ok:false});
    status(res.ok ? 'Adapter optimized' : 'Optimization failed: ' + (res.error || ''));
  });

  // Server Ping Monitor
  let pingMonitorInterval = null;
  $('#btnStartPingMonitor')?.addEventListener('click', () => {
    if (!api) return;
    if (pingMonitorInterval) clearInterval(pingMonitorInterval);
    const updatePings = () => {
      const data = safeApi(() => safeJson(api.GetPingStats()), {ok:false});
      if (data.ok) {
        $('#pingEu').textContent = data.eu || '--';
        $('#pingUs').textContent = data.us || '--';
        $('#pingRu').textContent = data.ru || '--';
        $('#pingSteam').textContent = data.steam || '--';
      }
    };
    updatePings();
    pingMonitorInterval = setInterval(updatePings, 3000);
    status('Ping monitor started');
  });
  $('#btnStopPingMonitor')?.addEventListener('click', () => {
    if (pingMonitorInterval) { clearInterval(pingMonitorInterval); pingMonitorInterval = null; }
    status('Ping monitor stopped');
  });

  // ============================================================
  // SETTINGS
  // ============================================================
  $('#backupBtn')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.CreateBackup()), {ok:false});
    status(res.ok ? 'Backup created' : 'Backup failed');
    refreshBackups();
  });

  $('#restoreBtn')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.RestoreLatestBackup()), {ok:false});
    status(res.ok ? 'Restored' : 'Restore failed');
    if (res.ok) loadConfig();
  });

  $('#openFolderBtn')?.addEventListener('click', () => { if (api) safeApi(() => api.OpenGameFolder()); });
  $('#refreshStatusBtn')?.addEventListener('click', () => { refreshAppInfo(); status('Status refreshed'); });

  $('#sendReportDiscordBtn')?.addEventListener('click', async () => {
    if (!api) return;
    const comment = $('#reportComment')?.value || '';
    status('Generating report...');
    const gen = safeApi(() => safeJson(api.GenerateReport(comment)), {ok:false});
    if (!gen.ok) { status('Report generation failed'); return; }
    status('Sending to Discord...');
    const res = safeApi(() => safeJson(api.SendReportToDiscord('', gen.path)), {ok:false});
    if (res.ok) {
      $('#reportPath').textContent = 'Report sent: ' + gen.path;
      status('Report sent to Discord');
    } else {
      $('#reportPath').textContent = 'Discord send failed: ' + (res.error || '');
      status('Discord send failed');
    }
  });

  $('#testWebhookBtn')?.addEventListener('click', async () => {
    if (!api) return;
    const webhook = $('#discordWebhookUrl')?.value?.trim() || '';
    if (!webhook) { status('Enter webhook URL'); return; }
    status('Testing webhook...');
    const res = safeApi(() => safeJson(api.TestDiscordWebhook(webhook)), {ok:false});
    status(res.ok ? 'Webhook test sent' : 'Webhook test failed: ' + (res.error || ''));
  });

  function refreshBackups() {
    if (!api) return;
    const list = safeApi(() => safeJson(api.ListBackups()), []);
    const el = $('#backupList');
    if (!el) return;
    if (list.length) {
      el.innerHTML = list.map(b => `<div class="backup-item">${b}</div>`).join('');
    } else {
      el.innerHTML = '<div class="backup-item" style="color:var(--text-muted)">No backups yet</div>';
    }
  }

  function refreshAppInfo() {
    if (!api) return;
    const ver = safeApi(() => api.GetAppVersion());
    if (ver && $('#appVersion')) $('#appVersion').textContent = ver;
    const game = safeApi(() => safeJson(api.DetectGame()), {found:false});
    if (game) {
      if ($('#gamePathVal')) $('#gamePathVal').textContent = game.found ? (game.path || 'Found') : 'Not found';
      if ($('#gameStatusVal')) $('#gameStatusVal').textContent = game.found ? (game.is_steam ? 'Steam version' : 'Standalone') : 'Install SCP:SL';
      if (game.found && game.path && $('#gamePathVal')) $('#gamePathVal').title = game.path;
    }
  }

  // ============================================================
  // STATE SYNC (readback from Windows / game)
  // ============================================================
  function syncSystemState() {
    if (!api) return;
    const st = safeApi(() => safeJson(api.GetSystemState()), {});
    $('#togGameMode')?.classList.toggle('on', !!st.game_mode);
    $('#togGameBar')?.classList.toggle('on', !!st.game_bar_disabled);
    $('#togCoreParking')?.classList.toggle('on', !!st.core_parking_disabled);
    $('#togHighPerf')?.classList.toggle('on', !!st.high_perf_plan);
    $('#togFso')?.classList.toggle('on', !!st.fso_disabled);
    $('#togHpet')?.classList.toggle('on', !!st.hpet_disabled);
    $('#togNagle')?.classList.toggle('on', !!st.nagle_disabled);
    $('#togSysmain')?.classList.toggle('on', !!st.sysmain_disabled);
    $('#togVisFx')?.classList.toggle('on', !!st.visual_effects_disabled);
  }

  function syncCheatsState() {
    if (!api) return;
    const st = safeApi(() => safeJson(api.GetCheatsState()), {});
    $('#nukeAcToggle')?.classList.toggle('on', !!st.ac_active);
    setNukeStatus('Ac', st.ac_active ? 'AC BYPASSED' : '', st.ac_active, false);
    $('#nukeBootToggle')?.classList.toggle('on', !!st.boot_patched);
    setNukeStatus('Boot', st.boot_patched ? 'BOOT.NUKED' : '', st.boot_patched, false);
    $('#nukeGgmToggle')?.classList.toggle('on', !!st.ggm_patched);
    setNukeStatus('Ggm', st.ggm_patched ? 'GGM DESTROYED' : '', st.ggm_patched, false);
    // Crash handler & analytics state inferred from file existence (simplified)
    const crashBak = safeApi(() => api?.GetCheatsState(), {});
    // Crash handler & analytics state sync
    const crashOn = safeApi(() => api?.GetCheatsState(), {});
    $('#nukeCrashToggle')?.classList.toggle('on', !!crashOn.crash_disabled);
    $('#nukeAnalyticsToggle')?.classList.toggle('on', !!crashOn.analytics_disabled);
  }

  function syncRknState() {
    if (!api) return;
    const st = safeApi(() => safeJson(api.GetRknState()), {});
    rknBypassActive = !!st.active;
    const zapret = safeApi(() => safeJson(api.IsZapretRunning()), {running:false});
    $('#infoZapret').textContent = zapret.running ? 'ON' : 'OFF';
    $('#infoZapret').classList.toggle('on', zapret.running);
    $('#infoZapret').classList.toggle('off', !zapret.running);
    updateNetStatus();
  }

  // ============================================================
  // INIT
  // ============================================================
  async function loadConfig() {
    if (!api) { status('API not available'); return; }
    const game = safeApi(() => safeJson(api.DetectGame()), {found:false});
    if (!game.found) { status('SCP:SL not found'); return; }
    const cfg = safeApi(() => safeJson(api.LoadConfig()), {});
    applyConfigToUI(cfg);
    const opts = safeApi(() => api.GetLaunchOptions(), '');
    $('.toggle-switch[data-launch="dx11"]')?.classList.toggle('on', opts.includes('-force-d3d11'));
    $('.toggle-switch[data-launch="vulkan"]')?.classList.toggle('on', opts.includes('-force-vulkan'));
    $('.toggle-switch[data-launch="opengl"]')?.classList.toggle('on', opts.includes('-force-opengl'));
    $('.toggle-switch[data-launch="fastest"]')?.classList.toggle('on', opts.includes('Fastest'));
    $('.toggle-switch[data-launch="borderless"]')?.classList.toggle('on', opts.includes('popupwindow'));
    $('.toggle-switch[data-launch="nogpu"]')?.classList.toggle('on', opts.includes('disable-gpu-skinning'));
    $('.toggle-switch[data-launch="nolog"]')?.classList.toggle('on', opts.includes('-nolog'));
    $('.toggle-switch[data-launch="nojoy"]')?.classList.toggle('on', opts.includes('-nojoy'));
    $('.toggle-switch[data-launch="high"]')?.classList.toggle('on', opts.includes('-high'));
    $('.toggle-switch[data-launch="malloc"]')?.classList.toggle('on', opts.includes('-malloc=system'));
    $('.toggle-switch[data-launch="allcores"]')?.classList.toggle('on', opts.includes('-USEALLAVAILABLECORES'));
    // Extract custom args (anything not matching known flags)
    if ($customLaunchArgs) {
      const known = ['-force-d3d11','-force-vulkan','-force-opengl','-screen-quality Fastest','-screen-fullscreen 0','-popupwindow','-disable-gpu-skinning','-nolog','-nojoy','-high','-malloc=system','-USEALLAVAILABLECORES'];
      const custom = opts.split(/\s+/).filter(p => p && !known.some(k => k.includes(p) || p.includes(k)));
      $customLaunchArgs.value = custom.join(' ');
    }
    updateLaunchPreview();
    refreshBackups();
    refreshAppInfo();
    syncSystemState();
    syncCheatsState();
    syncRknState();
    const webhook = safeApi(() => api.GetAppSetting('discord_webhook'), '');
    if ($('#discordWebhookUrl')) $('#discordWebhookUrl').value = webhook;
    const reportWebhook = safeApi(() => api.GetAppSetting('report_webhook'), '');
    if ($('#reportWebhookUrl')) $('#reportWebhookUrl').value = reportWebhook;
    updateSteamAuthUI();
    updateDiscordUserCard();
    updateSteamLinkUI();
    syncRoleFeatures();
    status('Ready');
  }

  $('#discordWebhookUrl')?.addEventListener('change', () => {
    if (!api) return;
    const url = $('#discordWebhookUrl')?.value?.trim() || '';
    safeApi(() => api.SaveAppSetting('discord_webhook', url));
  });

  $('#reportWebhookUrl')?.addEventListener('change', () => {
    if (!api) return;
    const url = $('#reportWebhookUrl')?.value?.trim() || '';
    safeApi(() => api.SaveAppSetting('report_webhook', url));
  });

  // Language switcher
  $$('.lang-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      $$('.lang-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      if (window.I18N) window.I18N.setLang(btn.dataset.lang);
    });
  });

  // Save button
  $('#btnSaveBar')?.addEventListener('click', saveConfig);
  $('#btnSaveBarFooter')?.addEventListener('click', saveConfig);
  window.saveConfigFromWpf = saveConfig;

  // Auto-refresh sync every 30s to keep UI aligned with real system/game state
  setInterval(() => {
    syncSystemState();
    syncCheatsState();
    syncRknState();
  }, 30000);

  // Steam Auth
  function updateSteamAuthUI() {
    const steamId = safeApi(() => api.GetSteamId(), '');
    const personaName = safeApi(() => api.GetSteamPersonaName(), '');
    const isDev = safeApi(() => api.IsSteamDeveloper(), false);
    const avatarUrl = safeApi(() => api.GetSteamAvatarUrl(), '');

    const loggedOut = $('#steamAuthLoggedOut');
    const loggedIn = $('#steamAuthLoggedIn');
    const pending = $('#steamAuthPending');
    const devBadge = $('#steamDevBadge');
    const avatarImg = $('#steamAvatarImg');
    const avatarFallback = $('#steamAvatarFallback');

    if (!steamId) {
      if (loggedOut) loggedOut.style.display = 'block';
      if (loggedIn) loggedIn.style.display = 'none';
      if (pending) pending.style.display = 'none';
    } else {
      if (loggedOut) loggedOut.style.display = 'none';
      if (loggedIn) loggedIn.style.display = 'block';
      if (pending) pending.style.display = 'none';
      if ($('#steamPersonaName')) $('#steamPersonaName').textContent = personaName || 'Steam User';
      if ($('#steamIdDisplay')) $('#steamIdDisplay').textContent = steamId;
      if (devBadge) devBadge.style.display = isDev ? 'inline-block' : 'none';

      if (avatarImg && avatarUrl) {
        avatarImg.src = avatarUrl;
        avatarImg.style.display = 'block';
        if (avatarFallback) avatarFallback.style.display = 'none';
        avatarImg.onerror = () => {
          avatarImg.style.display = 'none';
          if (avatarFallback) avatarFallback.style.display = 'flex';
        };
      } else if (avatarImg) {
        avatarImg.style.display = 'none';
        if (avatarFallback) avatarFallback.style.display = 'flex';
      }
    }
  }

  function syncRoleFeatures() {
    const role = safeApi(() => api.GetDiscordRole(), '');
    const isAdmin = role === 'Administrator' || role === 'Coder';
    const isCoder = role === 'Coder';

    // dev-only = Admin + Coder
    $$('.dev-only').forEach(el => {
      el.style.display = isAdmin ? '' : 'none';
    });

    // admin-only = Admin + Coder (same as dev-only for now)
    $$('.admin-only').forEach(el => {
      el.style.display = isAdmin ? '' : 'none';
    });

    // coder-only = Coder only
    $$('.coder-only').forEach(el => {
      el.style.display = isCoder ? '' : 'none';
    });
  }

  $('#steamLoginBtn')?.addEventListener('click', async () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.StartSteamAuth()), { error: 'Failed to start' });
    if (res.error) { status('Steam auth error: ' + res.error); return; }

    $('#steamAuthLoggedOut').style.display = 'none';
    $('#steamAuthPending').style.display = 'block';

    const pollInterval = setInterval(() => {
      const poll = safeApi(() => safeJson(api.PollSteamAuthResult()), { pending: true });
      if (poll.pending) return;

      clearInterval(pollInterval);
      $('#steamAuthPending').style.display = 'none';

      if (poll.ok) {
        status('Steam authorized: ' + (poll.steam_id || ''));
        updateSteamAuthUI();
      } else {
        status('Steam auth failed: ' + (poll.error || 'Unknown'));
        $('#steamAuthLoggedOut').style.display = 'block';
      }
    }, 1500);
  });

  $('#steamLogoutBtn')?.addEventListener('click', () => {
    if (!api) return;
    safeApi(() => api.SteamLogout());
    updateSteamAuthUI();
    status('Steam account unlinked');
  });

  // Discord User Card (main account display)
  function updateDiscordUserCard() {
    const loggedOut = $('#discordUserLoggedOut');
    const loggedIn = $('#discordUserLoggedIn');
    const displayName = $('#discordUserDisplayName');
    const idDisplay = $('#discordUserIdDisplay');
    const roleBadge = $('#discordUserRoleBadge');
    const devBadge = $('#discordUserDevBadge');
    const avatarImg = $('#discordUserAvatarImg');
    const avatarFallback = $('#discordUserAvatarFallback');
    if (!loggedOut || !loggedIn) return;

    const auth = safeApi(() => safeJson(api.GetSavedDiscordAuth()), { ok: false });
    if (auth.ok) {
      loggedOut.style.display = 'none';
      loggedIn.style.display = '';
      if (displayName) displayName.textContent = auth.username || 'Unknown';
      if (idDisplay) idDisplay.textContent = auth.id || '--';

      // Role badge — "блатные" роли
      if (roleBadge) {
        roleBadge.style.display = '';
        const role = auth.primary_role || 'Member';
        roleBadge.textContent = role;
        roleBadge.className = 'role-badge';
        if (role === 'Coder') {
          roleBadge.classList.add('coder');
          roleBadge.innerHTML = `<svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" style="margin-right:3px;"><polyline points="16 18 22 12 16 6"/><polyline points="8 6 2 12 8 18"/></svg> ${role}`;
        } else if (role === 'Administrator') {
          roleBadge.classList.add('admin');
          roleBadge.innerHTML = `<svg width="9" height="9" viewBox="0 0 24 24" fill="currentColor" style="margin-right:3px;"><path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/></svg> ${role}`;
        } else if (role === 'Beta-Tester') {
          roleBadge.classList.add('beta');
          roleBadge.innerHTML = `<svg width="9" height="9" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="margin-right:3px;"><path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/></svg> ${role}`;
        } else {
          roleBadge.textContent = auth.has_role ? 'VERIFIED' : 'NO ROLE';
          roleBadge.style.background = auth.has_role ? 'rgba(88,101,242,0.12)' : 'rgba(224,32,32,0.1)';
          roleBadge.style.color = auth.has_role ? '#5865F2' : '#e02020';
          roleBadge.style.borderColor = auth.has_role ? 'rgba(88,101,242,0.25)' : 'rgba(224,32,32,0.2)';
        }
      }

      // DEV badge — gold shimmer
      if (devBadge) {
        const isDev = auth.primary_role === 'Coder' || (auth.username || '').toLowerCase().includes('ziragi');
        devBadge.style.display = isDev ? 'inline-flex' : 'none';
      }

      // Avatar — use Discord CDN URL
      const avatarUrl = auth.avatar_url || '';
      if (avatarUrl && avatarImg) {
        avatarImg.src = avatarUrl;
        avatarImg.style.display = 'block';
        if (avatarFallback) avatarFallback.style.display = 'none';
      } else if (avatarFallback) {
        avatarFallback.style.display = 'flex';
        if (avatarImg) avatarImg.style.display = 'none';
      }
    } else {
      loggedOut.style.display = '';
      loggedIn.style.display = 'none';
    }
  }

  // Steam Link section (inside Discord Integration)
  function updateSteamLinkUI() {
    const loggedOut = $('#steamLinkLoggedOut');
    const loggedIn = $('#steamLinkLoggedIn');
    const personaName = $('#steamLinkPersonaName');
    const idDisplay = $('#steamLinkIdDisplay');
    const avatarImg = $('#steamLinkAvatarImg');
    const avatarFallback = $('#steamLinkAvatarFallback');
    if (!loggedOut || !loggedIn) return;

    const steamId = safeApi(() => api.GetSteamId(), '');
    if (steamId) {
      loggedOut.style.display = 'none';
      loggedIn.style.display = '';
      const name = safeApi(() => api.GetSteamPersonaName(), 'Steam User');
      if (personaName) personaName.textContent = name;
      if (idDisplay) idDisplay.textContent = steamId;

      const avatarUrl = safeApi(() => api.GetSteamAvatarUrl(), '');
      if (avatarUrl && avatarImg) {
        avatarImg.src = avatarUrl;
        avatarImg.style.display = '';
        if (avatarFallback) avatarFallback.style.display = 'none';
      } else if (avatarFallback) {
        avatarFallback.style.display = 'flex';
        if (avatarImg) avatarImg.style.display = 'none';
      }
    } else {
      loggedOut.style.display = '';
      loggedIn.style.display = 'none';
    }
  }

  // Event listeners
  $('#discordUserLoginBtn')?.addEventListener('click', () => {
    if (!api) return;
    const res = safeApi(() => safeJson(api.StartDiscordAuthFlow()), { ok: false });
    if (res.ok) status('Discord auth opened');
    else status('Failed to start auth flow');
  });
  $('#discordUserLogoutBtn')?.addEventListener('click', () => {
    if (!api) return;
    safeApi(() => api.DiscordLogout());
    updateDiscordUserCard();
    checkDiscordAccess();
    status('Discord disconnected');
  });

  // Steam Link
  $('#steamLinkBtn')?.addEventListener('click', () => {
    if (!api) return;
    safeApi(() => api.StartSteamAuth());
    status('Steam link opened');
    const pollInterval = setInterval(() => {
      const poll = safeApi(() => safeJson(api.PollSteamAuthResult()), { ok: false });
      if (!poll.done) return;
      clearInterval(pollInterval);
      if (poll.ok) {
        status('Steam linked: ' + (poll.steam_id || ''));
        updateSteamLinkUI();
      } else {
        status('Steam link failed');
      }
    }, 1500);
  });
  $('#steamLinkLogoutBtn')?.addEventListener('click', () => {
    if (!api) return;
    safeApi(() => api.SteamLogout());
    updateSteamLinkUI();
    status('Steam unlinked');
  });

  // Discord Rich Presence toggle
  (function() {
    const el = $('#togDiscordRP');
    if (!el || !api) return;
    let active = false;
    el.addEventListener('click', () => {
      active = !active;
      active ? el.classList.add('on') : el.classList.remove('on');
      const status = $('#discordRpStatus');
      if (active) {
        safeApi(() => api.DiscordRpInitialize());
        safeApi(() => api.DiscordRpSetActivity('MatrixHole-Engine', 'Optimizing game'));
        if (status) status.textContent = 'Showing in Discord';
      } else {
        safeApi(() => api.DiscordRpClear());
        if (status) status.textContent = 'Not connected';
      }
    });
  })();

  // Discord Access Gate
  function createGateParticles() {
    const container = $('#gateParticles');
    if (!container || container.dataset.particles) return;
    container.dataset.particles = '1';
    for (let i = 0; i < 24; i++) {
      const p = document.createElement('div');
      p.className = 'gate-particle';
      p.style.left = Math.random() * 100 + '%';
      p.style.animationDuration = (6 + Math.random() * 8) + 's';
      p.style.animationDelay = (Math.random() * 6) + 's';
      p.style.width = (2 + Math.random() * 3) + 'px';
      p.style.height = p.style.width;
      const colors = ['rgba(88,101,242,0.4)', 'rgba(235,69,158,0.3)', 'rgba(199,62,29,0.3)'];
      p.style.background = colors[Math.floor(Math.random() * colors.length)];
      container.appendChild(p);
    }
  }

  function checkDiscordAccess() {
    const gate = $('#discordGate');
    const app = $('#app');
    if (!gate || !app) return;
    if (!api) { gate.classList.remove('hidden'); app.style.display='none'; createGateParticles(); return; }
    const res = safeApi(() => safeJson(api.CheckDiscordAccess()), { ok: false });
    if (res.ok) {
      gate.classList.add('hidden');
      app.style.display='';
      app.style.opacity = '0';
      app.style.transform = 'scale(0.98)';
      requestAnimationFrame(() => {
        app.style.transition = 'opacity 0.4s ease, transform 0.4s ease';
        app.style.opacity = '1';
        app.style.transform = 'scale(1)';
      });
    } else {
      gate.classList.remove('hidden');
      app.style.display='none';
      createGateParticles();
      const err = $('#discordGateError');
      if (err && res.reason === 'no_role') {
        err.style.display = '';
        err.textContent = 'Access denied: your Discord account does not have the required role.';
      } else if (err) {
        err.style.display = 'none';
      }
    }
  }
  $('#btnDiscordGateLogin')?.addEventListener('click', () => {
    if (!api) return;
    safeApi(() => api.StartDiscordAuthFlow());
  });
  // Poll for Discord auth state every 3 seconds (gate + user card + role features)
  setInterval(() => {
    checkDiscordAccess();
    updateDiscordUserCard();
    syncRoleFeatures();
  }, 3000);
  checkDiscordAccess();

  // Update checker
  async function checkForUpdates() {
    if (!api) return;
    const data = safeApi(() => safeJson(api.CheckForUpdate()), { hasUpdate: false });
    if (data.hasUpdate) {
      $('#updateVersion').textContent = 'v' + data.version;
      $('#updateNotes').textContent = `Current: v${data.current || '1.0.0'}`;
      $('#updateBanner').classList.remove('hidden');
      $('#btnUpdateNow').dataset.version = data.version || '';
    }
  }
  $('#btnUpdateNow')?.addEventListener('click', async () => {
    const version = $('#btnUpdateNow').dataset.version;
    if (!version || !api) return;
    status('Downloading update...', 'info');
    const dl = safeApi(() => safeJson(api.DownloadUpdate(version)), { ok: false });
    if (dl.ok) {
      status('Update downloaded. Applying...', 'success');
      setTimeout(() => {
        safeApi(() => api.ApplyUpdate());
      }, 1500);
    } else {
      status('Update download failed: ' + (dl.error || 'unknown'), 'error');
    }
  });
  $('#btnDismissUpdate')?.addEventListener('click', () => {
    $('#updateBanner').classList.add('hidden');
  });
  checkForUpdates();

  // Init i18n
  if (window.I18N) {
    window.I18N.init('en');
    window.I18N._onLangChange = () => {
      refreshCleanup();
      loadLaunchParams();
    };
  }

  // ============================================================
  // NEW MODULES UI BINDINGS
  // ============================================================

  // Profile Manager
  async function refreshProfiles() {
    const list = $('#profileList');
    const active = $('#activeProfileDisplay');
    if (!api || !list) return;
    const profiles = safeApi(() => safeJson(api.GetProfiles(), []), []);
    const activeProfile = safeApi(() => safeJson(api.GetActiveProfile(), null), null);
    list.innerHTML = profiles.map(p => `<div style="display:flex;justify-content:space-between;align-items:center;padding:4px 0;border-bottom:1px solid var(--border);"><span>${p.name}${p.is_default ? ' (default)' : ''}</span><button class="btn-secondary" style="font-size:10px;padding:2px 8px;" onclick="window.setActiveProfile('${p.id}')">Activate</button></div>`).join('') || '<div style="color:var(--text-muted);">No profiles</div>';
    active.textContent = 'Active: ' + (activeProfile?.name || '--');
  }
  window.setActiveProfile = (id) => { safeApi(() => api.SetActiveProfile(id)); refreshProfiles(); status('Profile activated'); };
  $('#btnCreateProfile')?.addEventListener('click', () => {
    const name = prompt('Profile name:'); if (!name) return;
    const prof = { name, description: '', config: {} };
    safeApi(() => api.SaveProfile(JSON.stringify(prof))); refreshProfiles(); status('Profile created');
  });
  $('#btnRefreshProfiles')?.addEventListener('click', refreshProfiles);

  // Panic Reset
  $('#btnPanicReset')?.addEventListener('click', () => {
    if (!confirm('FULL RESET: restore game files, disable plugins, reset network/system. Are you sure?')) return;
    const res = safeApi(() => safeJson(api.ExecutePanicReset()), {});
    $('#panicResetStatus').textContent = res.message || JSON.stringify(res);
    status('Panic reset executed');
  });
  $('#btnSoftReset')?.addEventListener('click', () => {
    const res = safeApi(() => safeJson(api.ExecuteSoftReset()), {});
    $('#panicResetStatus').textContent = res.message || JSON.stringify(res);
    status('Soft reset executed');
  });

  // Mode toggles
  function bindModeToggle(id, apiGet, apiToggle) {
    const el = $(id);
    if (!el || !api) return;
    const update = () => { const st = safeApi(() => safeJson(apiGet()), {}); st.enabled ? el.classList.add('on') : el.classList.remove('on'); };
    el.addEventListener('click', () => { safeApi(() => apiToggle()); update(); });
    update();
  }
  bindModeToggle('#togHonestMode', () => api.IsHonestMode(), () => api.ToggleHonestMode());
  bindModeToggle('#togSystemOnly', () => api.IsSystemOnlyMode(), () => api.ToggleSystemOnlyMode());
  bindModeToggle('#togAfkMode', () => api.IsAfkMode(), () => api.ToggleAfkMode());
  bindModeToggle('#togGhostMode', () => api.GetGhostStatus(), () => api.ToggleGhostMode());

  // Diagnostics
  $('#btnExportDiagnostics')?.addEventListener('click', () => {
    const path = safeApi(() => safeJson(api.ExportDiagnostics()), {});
    $('#diagnosticsStatus').textContent = 'Exported: ' + (path.path || 'error');
    status('Diagnostics exported');
  });
  $('#btnCheckDependencies')?.addEventListener('click', () => {
    const res = safeApi(() => safeJson(api.CheckDependencies()), {});
    $('#dependencyStatus').textContent = res.allOk ? 'All dependencies OK' : 'Missing: ' + (res.missing?.map(m => m.Name).join(', ') || 'unknown');
  });

  // Disk Space
  $('#btnCheckDiskSpace')?.addEventListener('click', () => {
    const res = safeApi(() => safeJson(api.CheckGameDiskSpace()), {});
    $('#diskSpaceInfo').textContent = res.error ? res.error : `${res.drive} — ${res.free_gb} GB free / ${res.total_gb} GB total (${res.free_percent}%)`;
    if (res.warning) status(res.warning);
  });

  // Benchmark
  $('#btnStartBenchmark')?.addEventListener('click', () => {
    safeApi(() => api.RunBenchmark(60));
    $('#benchmarkStatus').textContent = 'Benchmark running...';
    status('Benchmark started');
  });
  $('#btnStopBenchmark')?.addEventListener('click', () => {
    safeApi(() => api.StopBenchmark());
    $('#benchmarkStatus').textContent = 'Benchmark stopped';
  });

  // Game Cleanup
  async function refreshCleanup() {
    const list = $('#cleanupList');
    if (!api || !list) return;
    const targets = safeApi(() => safeJson(api.GetCleanupTargets(), []), []);
    list.innerHTML = targets.map(t => {
      const tName = window.I18N ? window.I18N.t('cleanup_' + t.id) : t.name;
      const tBtn = window.I18N ? window.I18N.t('clean') : 'Clean';
      const dispName = tName !== 'cleanup_' + t.id ? tName : t.name;
      return `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid var(--border);"><span>${dispName}</span><button class="btn-secondary" style="font-size:10px;padding:2px 8px;" onclick="window.cleanupTarget('${t.id}')">${tBtn}</button></div>`;
    }).join('');
  }
  window.cleanupTarget = (id) => { safeApi(() => api.CleanupTarget(id)); refreshCleanup(); status('Cleanup done'); };
  $('#btnCleanupAll')?.addEventListener('click', () => { safeApi(() => api.CleanupAll()); refreshCleanup(); status('All cleanup done'); });
  $('#btnRefreshCleanup')?.addEventListener('click', refreshCleanup);

  // Conflict Detector
  $('#btnScanConflicts')?.addEventListener('click', () => {
    const res = safeApi(() => safeJson(api.ScanConflicts()), {});
    const list = $('#conflictList');
    if (!list) return;
    if (!res.hasConflicts) { list.innerHTML = '<div style="color:#4caf50;">No conflicts detected</div>'; return; }
    list.innerHTML = res.conflicts.map(c => `<div style="color:${c.severity === 'Critical' ? '#ff4444' : '#ffaa00'};padding:4px 0;border-bottom:1px solid var(--border);"><b>${c.DisplayName}</b> — ${c.Recommendation}</div>`).join('');
  });

  // Focus Assist
  $('#btnFocusAssistOn')?.addEventListener('click', () => { safeApi(() => api.ApplyGamingFocus()); status('Focus Assist ON'); });
  $('#btnFocusAssistOff')?.addEventListener('click', () => { safeApi(() => api.RestoreNormalFocus()); status('Focus Assist OFF'); });

  // Push Notifications
  async function refreshFavorites() {
    const list = $('#favoriteServersList');
    if (!api || !list) return;
    const favs = safeApi(() => safeJson(api.GetFavoriteServers(), []), []);
    list.innerHTML = favs.map(f => `<div style="display:flex;justify-content:space-between;padding:4px 0;border-bottom:1px solid var(--border);"><span>${f.name} (${f.ip})</span><button class="btn-secondary" style="font-size:10px;padding:2px 8px;" onclick="window.removeFavorite('${f.ip}')">Remove</button></div>`).join('') || '<div style="color:var(--text-muted);">No favorites</div>';
  }
  window.removeFavorite = (ip) => { safeApi(() => api.RemoveFavoriteServer(ip)); refreshFavorites(); };
  $('#btnAddFavorite')?.addEventListener('click', () => {
    const ip = $('#favServerIp')?.value; const name = $('#favServerName')?.value; const max = parseInt($('#favServerMax')?.value || '30');
    if (!ip || !name) return; safeApi(() => api.AddFavoriteServer(ip, name, max)); refreshFavorites();
  });
  $('#btnStartPushMonitor')?.addEventListener('click', () => { safeApi(() => api.StartPushMonitoring()); status('Push monitoring started'); });

  // Console Commands
  async function loadConsoleCommands() {
    const list = $('#consoleCmdList');
    if (!api || !list) return;
    const cmds = safeApi(() => safeJson(api.GetConsoleCommands(), []), []);
    list.innerHTML = cmds.map(c => `<div style="padding:4px 0;border-bottom:1px solid var(--border);cursor:pointer;" onclick="document.getElementById('consoleCmdInput').value='${c.command.replace(/'/g, "\\'")}'"><b>${c.name}</b> — ${c.description}</div>`).join('');
  }
  $('#btnSendConsoleCmd')?.addEventListener('click', () => {
    const input = $('#consoleCmdInput');
    if (!input?.value) return;
    const res = safeApi(() => safeJson(api.SendConsoleCommand('raw', input.value)), {});
    $('#consoleCmdStatus').textContent = res.ok ? 'Sent: ' + input.value : 'Error: ' + (res.error || 'unknown');
  });

  $('#btnSendInjectorCmd')?.addEventListener('click', () => {
    const input = $('#injectorCmdInput');
    if (!input?.value) return;
    const res = safeApi(() => safeJson(api.SendInjectorConsoleCommand(input.value)), {});
    status(res.ok ? 'Sent: ' + input.value : 'Error: ' + (res.error || 'unknown'));
  });

  // ========== Injector ==========
  async function refreshInjectorStatus() {
    const res = safeApi(() => safeJson(api.IsDllInjected()), { injected: false });
    const el = $('#injectorStatus');
    if (el) el.textContent = res.injected ? 'Status: Injected (press INSERT in-game)' : 'Status: Not injected';
  }
  $('#btnInjectDll')?.addEventListener('click', async () => {
    status('Injecting DLL...');
    const res = safeApi(() => safeJson(api.InjectDll()), { ok: false, error: 'unknown' });
    status(res.ok ? (res.status || 'Injected') : ('Inject failed: ' + (res.error || 'unknown')));
    refreshInjectorStatus();
  });
  $('#btnShowMenu')?.addEventListener('click', () => { safeApi(() => api.ShowInjectorMenu()); status('Menu shown'); });
  $('#btnHideMenu')?.addEventListener('click', () => { safeApi(() => api.HideInjectorMenu()); status('Menu hidden'); });
  $('#btnApplyAllPatches')?.addEventListener('click', () => { safeApi(() => api.ApplyAllInjectorPatches()); status('All patches applied'); });
  $('#btnRestoreAllPatches')?.addEventListener('click', () => { safeApi(() => api.RestoreAllInjectorPatches()); status('All patches restored'); });

  const patchButtons = {
    'btnPatchNoShadows': 'No Shadows',
    'btnPatchNoFog': 'No Fog',
    'btnPatchNoPost': 'No PostProcess',
    'btnPatchUnlockFps': 'Unlock FPS',
    'btnPatchLowParticles': 'Low Particles',
    'btnPatchNoBloom': 'No Bloom'
  };
  Object.entries(patchButtons).forEach(([id, patchName]) => {
    $(`#${id}`)?.addEventListener('click', () => {
      const res = safeApi(() => safeJson(api.ToggleInjectorPatch(patchName)), { ok: false });
      status(res.ok ? `Toggled: ${patchName}` : `Failed: ${patchName}`);
    });
  });
  refreshInjectorStatus();
  setInterval(refreshInjectorStatus, 5000);

  // Launch Param Builder
  async function loadLaunchParams() {
    const container = $('#launchParamBuilder');
    if (!api || !container) return;
    const flags = safeApi(() => safeJson(api.GetLaunchFlags(), []), []);
    container.innerHTML = flags.map(f => {
      const valWrap = f.requires_value
        ? `<span class="lp-val-wrap" id="lpw_${f.id}"><span class="lp-val-label">Value</span><input type="text" class="lp-val" id="lpv_${f.id}" placeholder="${f.default_value || ''}" value="${f.default_value || ''}" /></span>`
        : '';
      const tName = window.I18N ? window.I18N.t('lp_' + f.id) : f.name;
      const tDesc = window.I18N ? window.I18N.t('lp_' + f.id + '_desc') : f.description;
      const dispName = tName !== 'lp_' + f.id ? tName : f.name;
      const dispDesc = tDesc !== 'lp_' + f.id + '_desc' ? tDesc : f.description;
      return `<div class="lp-row"><input type="checkbox" id="lp_${f.id}" data-id="${f.id}" /><label for="lp_${f.id}"><b>${dispName}</b> — ${dispDesc}</label>${valWrap}</div>`;
    }).join('');
    // Listeners: checkbox toggles value input visibility + rebuilds preview
    container.querySelectorAll('input[type="checkbox"]').forEach(cb => {
      cb.addEventListener('change', () => {
        const wrap = $(`#lpw_${cb.dataset.id}`);
        if (wrap) wrap.classList.toggle('visible', cb.checked);
        updateLaunchPreview();
      });
    });
    // Text inputs also rebuild preview
    container.querySelectorAll('.lp-val').forEach(inp => {
      inp.addEventListener('input', updateLaunchPreview);
    });
  }
  function updateLaunchPreview() {
    const selected = {};
    $$('#launchParamBuilder input[type="checkbox"]').forEach(cb => {
      if (cb.checked) {
        const valInput = $(`#lpv_${cb.dataset.id}`);
        selected[cb.dataset.id] = (valInput && valInput.value) ? valInput.value : 'true';
      }
    });
    if (!api) return;
    const preview = safeApi(() => api.BuildLaunchString(JSON.stringify(selected)), '');
    const el = $('#launchParamPreview');
    if (el) el.textContent = preview || '';
  }

  // Advanced tab gate
  $('#btnUnlockAdvanced')?.addEventListener('click', () => {
    $('#advancedGate').style.display = 'none';
    $('#advancedContent').style.display = '';
  });

  // Advanced performance tweaks expander
  $('#toggleAdvancedPerf')?.addEventListener('click', () => {
    const content = $('#advancedPerfContent');
    const label = $('#advancedPerfLabel');
    if (!content || !label) return;
    const isHidden = content.style.display === 'none';
    content.style.display = isHidden ? '' : 'none';
    label.textContent = isHidden ? '[ Hide ]' : '[ Show ]';
  });

  // Non-intrusive toast for RKN auto-reset (called from C#)
  window.showRknAutoResetToast = (msg) => {
    status('⚠️ ' + msg);
    rknBypassActive = false;
    updateNetStatus();
    // Flash the network tab button to draw attention
    const netBtn = $('.nav-btn[data-tab="network"]');
    if (netBtn) {
      netBtn.style.color = '#c73e1d';
      setTimeout(() => { netBtn.style.color = ''; }, 4000);
    }
    // Highlight the net-status card inside network tab
    const netStatusCard = $('#netStatus')?.closest('.card');
    if (netStatusCard) {
      netStatusCard.style.borderColor = '#c73e1d';
      setTimeout(() => { netStatusCard.style.borderColor = ''; }, 4000);
    }
  };

  // Init new modules on load
  refreshProfiles();
  refreshCleanup();
  refreshFavorites();
  loadConsoleCommands();
  loadLaunchParams();

  // Init UI
  initToggles();
  initSliders();
  loadConfig();

  // ========== Licensing ==========
  function checkLicense() {
    const lic = safeApi(() => safeJson(api.GetLicenseStatus()), { licensed: false });
    const gate = $('#licenseGate');
    if (!lic.licensed && gate) {
      gate.classList.remove('hidden');
      const hwid = safeApi(() => safeJson(api.GetHwid()), { hwid: '---' });
      const hwidEl = $('#licenseHwid');
      if (hwidEl) hwidEl.textContent = hwid.hwid || '---';
    } else if (gate) {
      gate.classList.add('hidden');
    }
    return lic.licensed;
  }

  $('#btnActivateLicense')?.addEventListener('click', async () => {
    const input = $('#licenseKeyInput');
    if (!input?.value) return;
    const res = safeApi(() => safeJson(api.ActivateLicense(input.value.trim())), { ok: false });
    const errEl = $('#licenseGateError');
    if (res.ok) {
      if (errEl) { errEl.style.display = 'block'; errEl.style.color = 'var(--accent-success)'; errEl.textContent = 'License activated successfully!'; }
      setTimeout(() => { $('#licenseGate')?.classList.add('hidden'); }, 1200);
      // Update sidebar footer
      const footer = document.querySelector('.sidebar-footer');
      if (footer) {
        const licInfo = safeApi(() => safeJson(api.GetLicenseStatus()), { tier: 'standard', daysLeft: 0 });
        footer.innerHTML = `<div>&copy; 2025 MatrixHole</div><div style="margin-top:4px;color:var(--accent-success);font-weight:600;">${licInfo.tier?.toUpperCase()} — ${licInfo.daysLeft}d left</div>`;
      }
    } else {
      if (errEl) { errEl.style.display = 'block'; errEl.style.color = 'var(--accent-danger)'; errEl.textContent = res.error || 'Activation failed'; }
    }
  });

  // Onboarding
  const hasSeenOnboarding = localStorage.getItem('mh_onboarding');
  $('#btnCloseOnboarding')?.addEventListener('click', () => {
    $('#onboardingOverlay')?.classList.add('hidden');
    localStorage.setItem('mh_onboarding', '1');
  });

  // Auto-check license after splash
  setTimeout(() => {
    const licensed = checkLicense();
    if (licensed) {
      const footer = document.querySelector('.sidebar-footer');
      if (footer) {
        const licInfo = safeApi(() => safeJson(api.GetLicenseStatus()), { tier: 'standard', daysLeft: 0 });
        footer.innerHTML = `<div>&copy; 2025 MatrixHole</div><div style="margin-top:4px;color:var(--accent-success);font-weight:600;">${licInfo.tier?.toUpperCase()} — ${licInfo.daysLeft}d left</div>`;
      }
    }
    if (!hasSeenOnboarding) {
      $('#onboardingOverlay')?.classList.remove('hidden');
    }
  }, 2200);
})();
