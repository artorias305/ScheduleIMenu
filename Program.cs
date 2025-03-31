using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

class ScheduleIMenu
{
    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out int lpNumberOfBytesWritten);

    static string processName = "Schedule I";
    static string moduleName = "GameAssembly.dll";

    static void Main(string[] args)
    {
        Process process = Process.GetProcessesByName(processName).FirstOrDefault();
        if (process == null)
        {
            Console.WriteLine($"Process {processName} not found.");
            return;
        }

        IntPtr processHandle = process.Handle;
        IntPtr moduleBase = GetModuleBaseAddress(process, moduleName);
        if (moduleBase == IntPtr.Zero)
        {
            Console.WriteLine($"Module {moduleName} not found.");
            return;
        }

        Console.WriteLine($"Connected to {processName} at base address: 0x{moduleBase.ToInt64():X}");

        while (true)
        {
            Console.WriteLine("Enter command (e.g., balance 1000 or cash 5000): ");
            string command = Console.ReadLine().Trim();
            ProcessCommand(command, processHandle, moduleBase);
        }
    }

    static void ProcessCommand(string command, IntPtr processHandle, IntPtr moduleBase)
    {
        string[] commandParts = command.Split(' ');

        if (commandParts.Length < 2)
        {
            Console.WriteLine("Invalid command. Usage: <command> <value>");
            return;
        }

        string action = commandParts[0].ToLower();
        if (!float.TryParse(commandParts[1], out float value))
        {
            Console.WriteLine("Invalid value. Please provide a numeric value.");
            return;
        }

        switch (action)
        {
            case "balance":
                SetFeature(processHandle, moduleBase, "balance", value);
                break;
            case "cash":
                SetFeature(processHandle, moduleBase, "cash", value);
                break;
            default:
                Console.WriteLine($"Unknown command: {action}");
                break;
        }
    }

    static void SetFeature(IntPtr processHandle, IntPtr moduleBase, string feature, float newValue)
    {
        FeatureDetails featureDetails = GetFeatureDetails(feature);

        if (featureDetails == null)
        {
            Console.WriteLine($"Feature {feature} not found.");
            return;
        }

        IntPtr baseAddress = IntPtr.Add(moduleBase, featureDetails.BaseOffset);
        IntPtr finalPointer = FollowPointerChain(processHandle, baseAddress, featureDetails.Offsets);
        WriteMemory(processHandle, finalPointer, newValue);

        Console.WriteLine($"{feature} set to: {newValue}");
    }

    static FeatureDetails GetFeatureDetails(string feature)
    {
        switch (feature.ToLower())
        {
            case "balance":
                return new FeatureDetails
                {
                    BaseOffset = 0x3799D58,
                    Offsets = new int[] { 0x48, 0x3F0, 0x408, 0x20, 0xB8, 0x10, 0x128 }
                };
            case "cash":
                return new FeatureDetails
                {
                    BaseOffset = 0x37A3350,
                    Offsets = new int[] { 0x78, 0x270, 0x20, 0xB8, 0x20, 0x108, 0x38 }
                };
            default:
                return null;
        }
    }

    static IntPtr GetModuleBaseAddress(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
            {
                return module.BaseAddress;
            }
        }
        return IntPtr.Zero;
    }

    static IntPtr FollowPointerChain(IntPtr processHandle, IntPtr baseAddress, int[] offsets)
    {
        IntPtr address = baseAddress;
        foreach (int offset in offsets)
        {
            address = ReadMemory<IntPtr>(processHandle, address);
            if (address == IntPtr.Zero)
                throw new Exception("Invalid pointer resolution.");
            address = IntPtr.Add(address, offset);
        }
        return address;
    }

    static T ReadMemory<T>(IntPtr processHandle, IntPtr address) where T : struct
    {
        int size = Marshal.SizeOf(typeof(T));
        byte[] buffer = new byte[size];
        ReadProcessMemory(processHandle, address, buffer, size, out int bytesRead);
        if (bytesRead != size)
            throw new Exception("Failed to read memory.");
        return ByteArrayToStructure<T>(buffer);
    }

    static void WriteMemory(IntPtr processHandle, IntPtr address, float newValue)
    {
        byte[] buffer = BitConverter.GetBytes(newValue);
        WriteProcessMemory(processHandle, address, buffer, buffer.Length, out _);
    }

    static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
    {
        GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        T result = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        handle.Free();
        return result;
    }

    class FeatureDetails
    {
        public int BaseOffset { get; set; }
        public int[] Offsets { get; set; }
    }
}
