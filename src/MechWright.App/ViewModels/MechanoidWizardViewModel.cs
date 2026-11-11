using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MechWright.RimWorld;

namespace MechWright.App.ViewModels;

/// <summary>
/// The wizard never writes project files while fields are being edited.
/// Preview and acceptance both validate the same user-chosen blueprint.
/// </summary>
public sealed class MechanoidWizardViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    public string DefName { get; set; } = "MP_NewMechanoid";
    public string Label { get; set; } = "新机械体";
    public string WeightClass { get; set; } = "Medium";
    public string CP { get; set; } = "200";
    public string BodySize { get; set; } = "1";
    public string HealthScale { get; set; } = "1";
    public string MoveSpeed { get; set; } = "4.7";
    public string Mass { get; set; } = "85";
    public string SharpArmor { get; set; } = "0.40";
    public string BluntArmor { get; set; } = "0.20";
    public string HeatArmor { get; set; } = "2.00";
    public string BandwidthCost { get; set; } = "2";
    public IReadOnlyList<string> WeightClasses { get; } =
        ["Light", "Medium", "Heavy", "UltraHeavy"];
    private string _preview = "从上到下填写参数，点击“查看当前填写的数据”进行确认。";
    private string _error = "";
    public string Preview
    {
        get => _preview;
        private set { _preview = value; Changed(); }
    }
    public string Error
    {
        get => _error;
        private set { _error = value; Changed(); }
    }

    private static double Number(string name, string text)
    {
        if (!double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
            throw new ArgumentException(name + " 必须是有效数字（小数点使用 .）");
        return number;
    }

    public MechanoidBlueprint Build()
    {
        var bandwidth = int.TryParse(BandwidthCost,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : throw new ArgumentException("控制带宽必须为整数。");
        var blueprint = new MechanoidBlueprint(
            DefName.Trim(), Label.Trim(), WeightClass,
            Number("CP", CP), Number("身体大小", BodySize),
            Number("生命倍率", HealthScale), Number("移动速度", MoveSpeed),
            Number("质量", Mass), Number("锐器护甲", SharpArmor),
            Number("钝器护甲", BluntArmor), Number("热能护甲", HeatArmor), bandwidth);
        blueprint.GenerateFiles(); // Validate all inputs without project mutation.
        return blueprint;
    }

    public bool TryPreview()
    {
        try
        {
            var p = Build();
            Preview = $"机体：{p.Label}    类型：{p.WeightClass}    战斗点数：{p.CombatPower:0}\n" +
                $"身体大小：{p.BodySize:0.##}    生命倍率：{p.HealthScale:0.##}    速度：{p.MoveSpeed:0.##}\n" +
                $"质量：{p.Mass:0} kg    锐器护甲：{p.SharpArmor:0.##}    钝器护甲：{p.BluntArmor:0.##}\n" +
                $"热能护甲：{p.HeatArmor:0.##}    控制带宽：{p.BandwidthCost}\n" +
                "确认后自动生成机体、生成规则与身体结构三个关联定义。";
            Error = "";
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return false;
        }
    }

    public bool TryConfirm(out MechanoidBlueprint? blueprint)
    {
        try
        {
            blueprint = Build();
            Error = "";
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            blueprint = null;
            return false;
        }
    }
}
