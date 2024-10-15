using Chocolatey.PowerShell.Helpers;
using System.Management.Automation;

namespace Chocolatey.PowerShell.Shared
{
    public class BoolStringSwitchTransform : ArgumentTransformationAttribute
    {
        public override object Transform(EngineIntrinsics engineIntrinsics, object inputData)
        {
            switch (inputData)
            {
                case SwitchParameter s:
                    return s;
                case bool b:
                    return new SwitchParameter(b);
                default:
                    return new SwitchParameter(
                        !string.IsNullOrEmpty(PSHelper.ConvertTo<string>(inputData)));
            }
        }
    }
}
