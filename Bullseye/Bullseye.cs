using Bullseye.Native;

namespace Bullseye;

public class Bullseye : IDisposable {
    private readonly Interop interop;
    private readonly Hooks hooks;

    public Bullseye() {
        this.interop = new Interop();
        this.hooks = new Hooks(this.interop);
    }

    public void Dispose() {
        this.hooks.Dispose();
        this.interop.Dispose();
    }
}
