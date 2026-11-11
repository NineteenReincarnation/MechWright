using System.Collections.ObjectModel;
using System.Globalization;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

public sealed partial class MainWindowViewModel
{
    public ObservableCollection<WeaponProfileRow> WeaponProfiles { get; } = new();
    public bool ShowCombatEmpty => WeaponProfiles.Count == 0;

    private void RefreshCombat()
    {
        WeaponProfiles.Clear();
        if (_document != null && SelectedDefinition != null)
            foreach (var profile in WeaponAnalysis.Inspect(_document, SelectedDefinition.Entry))
                WeaponProfiles.Add(new WeaponProfileRow(profile));
        Notify(nameof(ShowCombatEmpty));
    }
}

public sealed class WeaponProfileRow(WeaponAnalysis.WeaponProfile profile)
{
    public string WeaponName => profile.WeaponDef;
    public string Verb => profile.VerbType;
    public string Projectile => string.IsNullOrEmpty(profile.ProjectileDef) ? "无显式弹丸" : profile.ProjectileDef;
    public string ShotCount => $"连发 {profile.BurstShots} 发";
    public string Range => $"射程：{Format(profile.Range)} 格";
    public string Damage => $"每弹伤害：{Format(profile.ShotDamage)}";
    public string Dps => $"理论 DPS：{Format(profile.TheoreticalDps)}（不计精度、护甲及移动）";
    public string Warmup => $"预热 {Format(profile.Warmup)} s";
    public string Burst => $"连发过程 {Format(profile.BurstDuration)} s";
    public string Cooldown => $"冷却 {Format(profile.Cooldown)} s";
    public double WarmupPercent => profile.WarmupPercentage ?? 0;
    public double BurstPercent => profile.BurstPercentage ?? 0;
    public double CooldownPercent => profile.CooldownPercentage ?? 0;

    private static string Format(double? number) =>
        number.HasValue ? number.Value.ToString("0.##", CultureInfo.InvariantCulture) : "未解析";
}
