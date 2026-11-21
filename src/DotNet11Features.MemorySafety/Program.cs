// ═════════════════════════════════════════════════════════════════════════════════════════════════
// 15 · New memory safety model (C# 15, PREVIEW)
// Documentation: docs/en/15-memory-safety.md · docs/fr/15-securite-memoire.md
//   dotnet run -c Release --project src/DotNet11Features.MemorySafety
//
// This project is compiled with the new rules (see the .csproj). The "before" code (C# 14) lives in the
// documentation: under the new rules it no longer compiles as is (errors CS9360, CS9362).
// ═════════════════════════════════════════════════════════════════════════════════════════════════

using System.Reflection;

Console.OutputEncoding = System.Text.Encoding.UTF8;
int failures = 0;
void Check(bool condition, string claim)
{
    Console.WriteLine($"     {(condition ? "✓" : "✗ FAILED:")} {claim}");
    if (!condition) failures++;
}

Console.WriteLine("── 15 · New memory safety model (C# 15, preview) ──");

int[] readings = [3, 1, 4, 1, 5, 9, 2, 6];

Console.WriteLine("\n   ▸ Pointers, & and fixed without \"unsafe\"; only the dereference is marked");
int sum = Calculations.Sum(readings);
Console.WriteLine($"     sum = {sum}");
Check(sum == readings.Sum(), "the only unsafe block of the method surrounds the start[i] read");

Console.WriteLine("\n   ▸ unsafe(expression): a single dangerous expression in the middle of safe code");
byte first = Calculations.FirstByte(0x11223344);
Console.WriteLine($"     first byte of 0x11223344 in memory: 0x{first:X2}");
Check(first == (BitConverter.IsLittleEndian ? 0x44 : 0x11), "the byte order in memory is read through a pointer");

Console.WriteLine("\n   ▸ A method marked unsafe: now it is the CALLER that must take responsibility");
int[] array = [10, 20, 30];
int value;
fixed (int* p = array)
{
    // Calculations.ReadUnchecked(p, 1);           ← error CS9362: must be used in an unsafe context
    value = unsafe(Calculations.ReadUnchecked(p, 1)); // the caller signs: "I guarantee that 1 < 3"
}
Console.WriteLine($"     ReadUnchecked(p, 1) = {value}");
Check(value == 20, "the call compiles only inside unsafe(…)");

Console.WriteLine("\n   ▸ What the compiler writes into the binary");
var attribute = Assembly.GetExecutingAssembly().GetCustomAttributesData()
    .Concat(typeof(Calculations).Module.GetCustomAttributesData())
    .FirstOrDefault(a => a.AttributeType.Name == "MemorySafetyRulesAttribute");
Console.WriteLine($"     {(attribute is null ? "(no attribute)" : attribute.ToString())}");
Check(attribute is not null, "the binary declares the rules it was compiled with");
var method = typeof(Calculations).GetMethod(nameof(Calculations.ReadUnchecked))!;
string marks = string.Join(", ", method.GetCustomAttributesData().Select(a => a.AttributeType.Name));
Console.WriteLine($"     attributes of ReadUnchecked: {(marks == "" ? "(none)" : marks)}");
Check(marks.Contains("RequiresUnsafeAttribute"), "an unsafe method carries [RequiresUnsafe]: other assemblies see the requirement");

Console.WriteLine();
Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} check(s) failed.");
return failures;

static class Calculations
{
    // Declaring a pointer, taking an address, fixed, sizeof: no unsafe context needed anymore.
    // Only the DEREFERENCE (*p, p[i], p->x) reads memory unchecked: only it is marked.
    public static int Sum(int[] values)
    {
        int total = 0;
        fixed (int* start = values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                unsafe { total += start[i]; }   // ← the only line to review carefully
            }
        }
        return total;
    }

    public static byte FirstByte(int value)
    {
        int* address = &value;                  // safe: we only take an address
        byte* b = (byte*)address;               // safe: a plain pointer conversion
        return unsafe(*b);                      // dangerous: memory read, isolated in unsafe(…)
    }

    /// <summary>Reads values[index] without bounds checking.</summary>
    /// <remarks>Safety: the caller guarantees that <paramref name="index"/> is inside the pointed array.</remarks>
    // "unsafe" on a member no longer makes its body unsafe: it means "requires an unsafe context at the
    // call site", like "unsafe fn" in Rust. The body must mark its own dangerous operations.
    public static unsafe int ReadUnchecked(int* values, int index) => unsafe(values[index]);
}
