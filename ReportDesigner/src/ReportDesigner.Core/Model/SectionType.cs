using System;
using System.Collections.Generic;
using System.Text;

namespace ReportDesigner.Core.Model;

/// <summary>
/// Defines all supported band / section types in a report, in render order.
/// </summary>
public enum SectionType
{
    /// <summary>Printed once at the very beginning of the report.</summary>
    ReportHeader,

    /// <summary>Printed at the top of every page.</summary>
    PageHeader,

    /// <summary>Printed when the value of the group field changes (before the group's rows).</summary>
    GroupHeader,

    /// <summary>Repeating band – one instance per DataTable row.</summary>
    Detail,

    /// <summary>Printed after all rows in a group have been rendered.</summary>
    GroupFooter,

    /// <summary>Printed at the bottom of every page.</summary>
    PageFooter,

    /// <summary>Printed once at the very end of the report.</summary>
    ReportFooter,
}