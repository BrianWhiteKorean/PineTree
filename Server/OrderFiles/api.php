<?php
declare(strict_types=1);
// Files and credentials live outside Apache's document root.
$settings = require '/opt/lampp/etc/pinetree-order-files.php';
header('Cache-Control: no-store');
header('X-Content-Type-Options: nosniff');
function fail(int $code, string $message): never {
    http_response_code($code);
    header('Content-Type: application/json; charset=utf-8');
    echo json_encode(['error' => $message]);
    exit;
}
if (empty($_SERVER['HTTPS']) || strtolower((string)$_SERVER['HTTPS']) === 'off') fail(403, 'HTTPS required');
if (!hash_equals($settings['token'], (string)($_SERVER['HTTP_X_PINETREE_TOKEN'] ?? ''))) fail(401, 'Authentication required');
$id = (string)($_GET['id'] ?? '');
if (!preg_match('/\A([a-f0-9]{64})\.(pdf|eps|ai|png|jpg|jpeg)\z/D', $id, $match)) fail(400, 'Invalid file identifier');
$path = $settings['directory'] . '/' . $id;
$method = $_SERVER['REQUEST_METHOD'];
if ($method === 'GET') {
    if (!is_file($path)) fail(404, 'File not found');
    header('Content-Type: application/octet-stream');
    header('Content-Length: ' . filesize($path));
    header('Content-Disposition: attachment; filename="' . $id . '"');
    readfile($path);
    exit;
}
if ($method !== 'POST') { header('Allow: GET, POST'); fail(405, 'Method not allowed'); }
$expectedLength = filter_var($_SERVER['CONTENT_LENGTH'] ?? '', FILTER_VALIDATE_INT);
if ($expectedLength === false || $expectedLength <= 0 || $expectedLength > $settings['maxBytes']) fail(413, 'Invalid file size');
// Content-addressed identifiers make identical uploads reusable without overwriting other orders.
$partial = tempnam($settings['directory'], '.upload-');
if ($partial === false) fail(500, 'Cannot allocate upload');
register_shutdown_function(static function () use ($partial) { if (is_file($partial)) unlink($partial); });
$source = fopen('php://input', 'rb');
$target = fopen($partial, 'wb');
if ($source === false || $target === false) fail(500, 'Cannot open upload');
$hash = hash_init('sha256');
$total = 0;
while (!feof($source)) {
    $buffer = fread($source, 1024 * 1024);
    if ($buffer === false) fail(400, 'Cannot read upload');
    $total += strlen($buffer);
    if ($total > $expectedLength) fail(413, 'Size mismatch');
    hash_update($hash, $buffer);
    if (fwrite($target, $buffer) !== strlen($buffer)) fail(507, 'Storage full');
}
fclose($source);
fclose($target);
$sha256 = hash_final($hash);
if ($total !== $expectedLength || !hash_equals($match[1], $sha256)) fail(422, 'File integrity mismatch');
chmod($partial, 0600);
// Atomic replacement can only contain the identical, verified content for this hash.
if (!rename($partial, $path)) fail(500, 'Cannot commit upload');
header('Content-Type: application/json; charset=utf-8');
echo json_encode(['FileId' => $id, 'Length' => $total, 'Sha256' => $sha256]);
