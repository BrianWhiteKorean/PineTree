"""Run from the workspace with paramiko installed. Does not print credentials.

Creates only the order-files API, its private storage/config and the additive DB
configuration table. Existing file bytes, orders and SSH/Apache configuration stay intact.
"""
import base64
import hashlib
import json
import re
import shlex
from pathlib import Path
import paramiko

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
memo = (ROOT / '3.개발/PineTree/개발메모.txt').read_text(encoding='utf-8-sig')
host = re.search(r'(?m)^\s*(\d+\.\d+\.\d+\.\d+)\s*$', memo).group(1)
user, password = re.search(r'(?m)^\s*(\w+)\s*/\s*(\S+)\s*$', memo).groups()
db_source = (ROOT / '3.개발/PineTree/Classes/DB.cs').read_text(encoding='utf-8-sig', errors='replace')
db_password = re.search(r'string sqlPassword\s*=\s*"([^"]+)"', db_source).group(1)
client = paramiko.SSHClient()
client.load_system_host_keys()
pin_path = HERE / 'server-host-key.sha256'
class PinnedOrFirstUse(paramiko.MissingHostKeyPolicy):
    def missing_host_key(self, ssh, name, key):
        pin = base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip('=')
        if pin_path.exists() and pin_path.read_text().strip() != pin:
            raise RuntimeError('Server host key changed; verify identity before deployment')
        if not pin_path.exists():
            pin_path.write_text(pin + '\n')
client.set_missing_host_key_policy(PinnedOrFirstUse())
client.connect(host, username=user, password=password, timeout=20)
def run(command, data=None):
    stdin, stdout, stderr = client.exec_command(command)
    if data is not None:
        stdin.write(data)
        stdin.flush()
        stdin.channel.shutdown_write()
    output = stdout.read().decode('utf-8')
    error = stderr.read().decode('utf-8')
    if stdout.channel.recv_exit_status() != 0:
        raise RuntimeError('Server deployment step failed: ' + error[:300])
    return output
try:
    run('install -d -o daemon -g daemon -m 0700 /var/lib/pinetree/order-files; '
        'install -d -o root -g root -m 0755 /opt/lampp/htdocs/pinetreefiles')
    config = '/opt/lampp/etc/pinetree-order-files.php'
    # Preserve the server token on redeployment. No token is written into source code.
    run("test -f " + config + " || /opt/lampp/bin/php -r " + shlex.quote(
        "$s=['token'=>bin2hex(random_bytes(32)), 'directory'=>'/var/lib/pinetree/order-files', 'maxBytes'=>2147483647];"
        "file_put_contents('/opt/lampp/etc/pinetree-order-files.php', '<?php return '.var_export($s,true).';');"))
    run('chown root:daemon ' + config + '; chmod 0640 ' + config)
    sftp = client.open_sftp()
    stage = '/opt/lampp/htdocs/pinetreefiles/api.php.staging'
    sftp.put(str(HERE / 'api.php'), stage)
    sftp.chmod(stage, 0o644)
    run('/opt/lampp/bin/php -l ' + stage)
    sftp.posix_rename(stage, '/opt/lampp/htdocs/pinetreefiles/api.php')
    sftp.close()
    payload = base64.b64encode(json.dumps({
        'password': db_password,
        'migration': (HERE / 'OrderFileStorageSettings.sql').read_text(encoding='utf-8-sig'),
        'endpoint': 'https://yeowoosai.co.kr/pinetreefiles/api.php'
    }).encode()).decode()
    php = "<?php $p=json_decode(base64_decode('%s'),true);" % payload
    php += """
    mysqli_report(MYSQLI_REPORT_ERROR | MYSQLI_REPORT_STRICT);
    $db = new mysqli('127.0.0.1', 'root', $p['password'], 'PineTree');
    $db->set_charset('utf8mb4');
    $db->query($p['migration']);
    $config = require '/opt/lampp/etc/pinetree-order-files.php';
    $stmt = $db->prepare("INSERT INTO OrderFileStorageSettings(StorageKey,EndpointUrl,ApiToken,IsActive)
        VALUES('BANNER',?,?,1) ON DUPLICATE KEY UPDATE EndpointUrl=VALUES(EndpointUrl),ApiToken=VALUES(ApiToken),IsActive=1");
    $stmt->bind_param('ss', $p['endpoint'], $config['token']); $stmt->execute();
    echo 'Storage settings configured; existing order tables preserved.';
    """
    print(run('/opt/lampp/bin/php', php))
    print(run("stat -c '%a %U %G %n' /var/lib/pinetree/order-files /opt/lampp/htdocs/pinetreefiles/api.php"))
finally:
    client.close()
