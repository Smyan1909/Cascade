const { exec } = require('child_process');
const { promisify } = require('util');
const execAsync = promisify(exec);

async function getWslHostIp() {
  try {
    const { stdout } = await execAsync("ip route show default | awk '{print $3}'");
    const ip = stdout.trim();
    if (ip && /^\d+\.\d+\.\d+\.\d+$/.test(ip)) {
      return ip;
    }
  } catch (e) { }

  try {
    const { stdout } = await execAsync("cat /etc/resolv.conf | grep nameserver | awk '{print $2}'");
    const ip = stdout.trim();
    if (ip && /^\d+\.\d+\.\d+\.\d+$/.test(ip)) {
      return ip;
    }
  } catch (e) { }
  return null;
}

getWslHostIp().then(console.log);
