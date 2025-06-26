using HandheldCompanion.Controllers.SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Controllers.Dummies
{
    public class DummyXbox360Controller : Xbox360Controller
    {
        public override bool IsVirtual() => true;
        public override bool IsDummy() => true;
    }
}
