using TmrOverlay.App.Overlays.BrowserSources;

namespace TmrOverlay.App.Overlays.PitService;

internal static class PitServiceBrowserSource
{
    public static BrowserOverlayPage Page { get; } = new(
        id: PitServiceOverlayDefinition.Definition.Id,
        title: PitServiceOverlayDefinition.Definition.DisplayName,
        canonicalRoute: "/overlays/pit-service",
        fadeWhenTelemetryUnavailable: PitServiceOverlayDefinition.Definition.FadeWhenLiveTelemetryUnavailable,
        script: Script);

    private const string Script = """
    const pitPreviousValues = new Map();
    const pitHighlightUntil = new Map();

    TmrBrowserOverlay.register({
      render(live) {
        const pit = live?.models?.fuelPit || {};
        const release = pitRelease(pit);
        const values = {
          release: release.value,
          location: locationText(pit),
          service: serviceText(pit),
          status: pitStatusText(pit.pitServiceStatus),
          fuel: fuelRequestText(pit),
          repair: repairText(pit),
          tires: tireText(pit),
          fastRepair: fastRepairText(pit)
        };
        contentEl.innerHTML = `
          <div class="grid">
            ${metric('Release', values.release)}
            ${metric('Location', values.location)}
            ${pitMetric('Service', values.service, pitValueChanged('service', values.service))}
            ${metric('Pit status', values.status)}
            ${pitMetric('Fuel req', values.fuel, pitValueChanged('fuel', values.fuel))}
            ${pitMetric('Repair', values.repair, pitValueChanged('repair', values.repair))}
            ${pitMetric('Tires', values.tires, pitValueChanged('tires', values.tires))}
            ${pitMetric('Fast repair', values.fastRepair, pitValueChanged('fast-repair', values.fastRepair))}
          </div>`;
        setStatus(live, pit.hasData ? release.status : 'waiting for pit');
      }
    });

    const pitStatus = {
      none: 0,
      inProgress: 1,
      complete: 2,
      tooFarLeft: 100
    };
    const tireMask = 0x0f;
    const fuelFlag = 0x10;
    const tearoffFlag = 0x20;
    const fastRepairFlag = 0x40;

    function pitRelease(pit) {
      if (isPitStatusError(pit.pitServiceStatus)) {
        return { value: `RED - ${pitStatusText(pit.pitServiceStatus)}`, status: 'pit stall error' };
      }
      if (pit.pitServiceStatus === pitStatus.complete) {
        return { value: 'GREEN - go', status: 'release ready' };
      }
      if (isServiceActive(pit)) {
        return { value: 'RED - service active', status: 'hold' };
      }
      if (hasRequiredRepair(pit)) {
        return { value: 'RED - repair active', status: 'hold' };
      }
      if (hasOptionalRepair(pit)) {
        return { value: 'YELLOW - optional repair', status: 'optional repair' };
      }
      if (pit.playerCarInPitStall) {
        return { value: pit.pitServiceStatus == null ? 'GREEN - go (inferred)' : 'GREEN - go', status: 'release ready' };
      }
      if (pit.onPitRoad || pit.teamOnPitRoad === true) {
        return { value: 'pit road', status: 'on pit road' };
      }
      return hasRequestedService(pit)
        ? { value: 'armed', status: 'service requested' }
        : { value: '--', status: 'pit ready' };
    }

    function locationText(pit) {
      if (pit.playerCarInPitStall) return 'player in stall';
      if (pit.onPitRoad && pit.teamOnPitRoad === true) return 'player/team on pit road';
      if (pit.onPitRoad) return 'player on pit road';
      if (pit.teamOnPitRoad === true) return 'team on pit road';
      return 'off pit road';
    }

    function serviceText(pit) {
      let service = serviceFlagsText(pit.pitServiceFlags);
      if ((service === '--' || service === 'none') && positive(pit.pitServiceFuelLiters)) {
        service = 'fuel';
      }
      if (isServiceActive(pit)) {
        return service === '--' || service === 'none' ? 'active' : `active | ${service}`;
      }
      if (hasRequestedService(pit)) {
        return service === '--' || service === 'none' ? 'requested' : `requested | ${service}`;
      }
      return service;
    }

    function serviceFlagsText(flags) {
      if (!Number.isFinite(flags)) return '--';
      const value = flags | 0;
      const parts = [];
      const tires = tireServiceCount(value);
      if (tires === 4) parts.push('tires');
      else if (tires > 0) parts.push(`${tires} tires`);
      if ((value & fuelFlag) !== 0) parts.push('fuel');
      if ((value & tearoffFlag) !== 0) parts.push('tearoff');
      if ((value & fastRepairFlag) !== 0) parts.push('fast repair');
      return parts.length ? parts.join(', ') : 'none';
    }

    function fuelRequestText(pit) {
      return positive(pit.pitServiceFuelLiters) ? `${pit.pitServiceFuelLiters.toFixed(1)} L` : '--';
    }

    function repairText(pit) {
      return joinParts([
        hasRequiredRepair(pit) ? `${pit.pitRepairLeftSeconds.toFixed(0)}s required` : null,
        hasOptionalRepair(pit) ? `${pit.pitOptRepairLeftSeconds.toFixed(0)}s optional` : null
      ]);
    }

    function tireText(pit) {
      const tires = tireServiceCount(pit.pitServiceFlags);
      const service = tires === 4 ? 'four tires' : tires > 0 ? `${tires} tires` : null;
      const sets = Number.isFinite(pit.tireSetsUsed) && pit.tireSetsUsed >= 0 ? `${pit.tireSetsUsed} sets used` : null;
      return joinParts([service, sets]);
    }

    function fastRepairText(pit) {
      return joinParts([
        hasFastRepairSelected(pit.pitServiceFlags) ? 'selected' : null,
        Number.isFinite(pit.fastRepairUsed) && pit.fastRepairUsed >= 0 ? `local ${pit.fastRepairUsed}` : null,
        Number.isFinite(pit.teamFastRepairsUsed) && pit.teamFastRepairsUsed >= 0 ? `team ${pit.teamFastRepairsUsed}` : null
      ]);
    }

    function pitStatusText(status) {
      switch (status) {
        case null:
        case undefined:
          return '--';
        case pitStatus.none:
          return 'none';
        case pitStatus.inProgress:
          return 'in progress';
        case pitStatus.complete:
          return 'complete';
        case 100:
          return 'too far left';
        case 101:
          return 'too far right';
        case 102:
          return 'too far forward';
        case 103:
          return 'too far back';
        case 104:
          return 'bad angle';
        case 105:
          return 'cannot repair';
        default:
          return `status ${status}`;
      }
    }

    function isServiceActive(pit) {
      return pit.pitServiceStatus === pitStatus.inProgress || pit.pitstopActive === true;
    }

    function hasRequestedService(pit) {
      return (Number.isFinite(pit.pitServiceFlags) && pit.pitServiceFlags !== 0) || positive(pit.pitServiceFuelLiters);
    }

    function hasRequiredRepair(pit) {
      return positive(pit.pitRepairLeftSeconds);
    }

    function hasOptionalRepair(pit) {
      return positive(pit.pitOptRepairLeftSeconds);
    }

    function hasFastRepairSelected(flags) {
      return Number.isFinite(flags) && ((flags | 0) & fastRepairFlag) !== 0;
    }

    function isPitStatusError(status) {
      return Number.isFinite(status) && status >= pitStatus.tooFarLeft;
    }

    function tireServiceCount(flags) {
      if (!Number.isFinite(flags)) return 0;
      let count = 0;
      const tireFlags = (flags | 0) & tireMask;
      for (let bit = 1; bit <= 0x08; bit <<= 1) {
        if ((tireFlags & bit) !== 0) count++;
      }
      return count;
    }

    function positive(value) {
      return Number.isFinite(value) && value > 0;
    }

    function joinParts(parts) {
      const available = parts.filter((part) => part && part !== '--');
      return available.length ? available.join(' | ') : '--';
    }

    function pitValueChanged(key, value) {
      const normalized = String(value ?? '').trim();
      const previous = pitPreviousValues.get(key);
      const now = Date.now();
      if (previous != null && previous !== normalized) {
        pitHighlightUntil.set(key, now + 30000);
      }
      pitPreviousValues.set(key, normalized);
      return (pitHighlightUntil.get(key) ?? 0) >= now;
    }

    function pitMetric(label, value, highlighted) {
      if (!highlighted) return metric(label, value);
      return `
        <div class="metric" style="border-color: rgba(98, 199, 255, 0.55); background: rgba(98, 199, 255, 0.10);">
          <div class="label">${escapeHtml(label)}</div>
          <div class="value" style="color: #68c1ff;">${escapeHtml(value)}</div>
        </div>`;
    }
    """;
}
