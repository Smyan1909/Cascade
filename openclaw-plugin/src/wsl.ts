import * as os from 'os';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

/**
 * Detect if OpenClaw is running inside Windows Subsystem for Linux (WSL)
 */
export function isWsl(): boolean {
  if (process.platform !== 'linux') {
    return false;
  }
  try {
    const release = os.release().toLowerCase();
    return release.includes('microsoft') || release.includes('wsl');
  } catch {
    return false;
  }
}

/**
 * Automatically fetch the Windows host IP address when running inside WSL.
 * This is crucial because `localhost` inside WSL refers to the Linux VM,
 * not the Windows host where the C# Body or Windows Python is running.
 */
export async function getWslHostIp(): Promise<string | null> {
  if (!isWsl()) return null;

  try {
    // Strategy 1: Check the default IP route (more reliable for actual network routing)
    const { stdout } = await execAsync("ip route show default | awk '{print $3}'");
    const ip = stdout.trim();
    if (ip && /^\d+\.\d+\.\d+\.\d+$/.test(ip)) {
      // In newer WSL2 versions, 10.255.255.254 might be returned by DNS but it's just a proxy.
      // The default route is the true host IP.
      return ip;
    }
  } catch (e) {
    // Ignore and try strategy 2
  }

  try {
    // Strategy 2: Read /etc/resolv.conf which points to the WSL virtual switch gateway
    const { stdout } = await execAsync("cat /etc/resolv.conf | grep nameserver | awk '{print $2}'");
    const ip = stdout.trim();
    if (ip && /^\d+\.\d+\.\d+\.\d+$/.test(ip)) {
      return ip;
    }
  } catch (e) {
    console.debug('Cascade auto-discovery failed to resolve WSL host IP:', e);
  }

  return null;
}
