namespace ShipGame.Gameplay;

/// <summary>Player-facing effect blurbs for loadout, research, and station upgrades.</summary>
public static class MetaItemDescriptions
{
    public static string For(string id) => id switch
    {
        ModuleCatalog.WeaponPulse => "Rapid bolts. Reliable discrete fire with no ammo.",
        ModuleCatalog.WeaponBeam => "Continuous hitscan beam. Overheats if held too long.",
        ModuleCatalog.WeaponSeeker => "Dual missiles: home in-cone, or fly straight when no lock.",
        ModuleCatalog.MiningLaser => "Continuous mining beam for asteroid cells.",
        ModuleCatalog.MiningCharge => "Aimed seismic blast: mining AoE plus light combat damage.",
        ModuleCatalog.ShieldCapacitor => "Balanced shield capacity and recharge.",
        ModuleCatalog.ShieldReflective => "Lower capacity; reflects 20% of projectile damage.",
        ModuleCatalog.EngineVector => "Dash mobility with brief invulnerability.",
        ModuleCatalog.EngineBlink => "Longer blink teleport through entities.",
        ModuleCatalog.UtilityTractor => "Pulls ferrite, lumen, and data-core pickups from asteroids and wrecks.",
        ModuleCatalog.UtilityDrone => "Orbiting Firefly that fires on nearby hostiles.",

        ResearchCatalog.HullReinforcement => "Base hull +15 on subsequent runs.",
        ResearchCatalog.ShieldReflective => "Unlocks Bastion Reflective Screen.",
        ResearchCatalog.WeaponBeam => "Unlocks Helios Beam Emitter.",
        ResearchCatalog.WeaponSeeker => "Unlocks Warden Seeker Rack.",
        ResearchCatalog.MiningSeismic => "Unlocks Seismic Charge mining module.",
        ResearchCatalog.MiningAssay => "Ferrite cell yield +15% (rounded up).",
        ResearchCatalog.EngineTuning => "Maximum speed +8%.",
        ResearchCatalog.EngineBlink => "Unlocks Comet Blink Drive.",
        ResearchCatalog.UtilityDrone => "Unlocks Firefly Scout Drone.",
        ResearchCatalog.TractorCalibration => "Base pickup radius +35.",
        ResearchCatalog.NavIonVeil => "Grants travel access to the Ion Veil.",
        ResearchCatalog.RecoveryProtocols => "Failed runs keep 50% of Ferrite.",

        "UPG_OVERCHARGED_MUNITIONS" => "Weapon damage +20%.",
        "UPG_RAPID_CYCLING" => "Fire rate / beam tick output +18%.",
        "UPG_FORKED_OUTPUT" => "Extra angled shot: pulse 85%, beam 45%, seeker 60%.",
        "UPG_PENETRATING_FIELD" => "Pulse pierces once; beam passes one target; seeker blast radius 55.",
        "UPG_SHIELD_RESERVOIR" => "Maximum and current shield +30.",
        "UPG_FAST_REBOOT" => "Shield delay -1.0s; recharge rate +20%.",
        "UPG_REINFORCED_FRAME" => "Maximum and current hull +25.",
        "UPG_THRUSTER_OVERCLOCK" => "Maximum speed +15%.",
        "UPG_MOBILITY_LOOP" => "Dash/blink cooldown -30%.",
        "UPG_FRACTURE_LENS" => "Mining damage +30%; Ferrite yield +20%.",
        "UPG_MAGNETIC_SWEEP" => "Pickup radius +90; pull speed +50%.",
        "UPG_SHOCK_TRANSIT" => "Mobility endpoint shockwave: radius 90, 20 damage.",

        _ => "No description available."
    };
}
