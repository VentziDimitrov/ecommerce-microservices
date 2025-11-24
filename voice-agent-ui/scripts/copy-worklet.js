#!/usr/bin/env node

/**
 * Build script to copy AudioWorklet processor to public folder
 * This ensures the worklet is available at runtime
 */

const fs = require('fs');
const path = require('path');

const sourceFile = path.join(__dirname, '..', 'src', 'services', 'audio', 'pcm-processor.worklet.js');
const targetFile = path.join(__dirname, '..', 'public', 'pcm-processor.js');

try {
  // Ensure public directory exists
  const publicDir = path.dirname(targetFile);
  if (!fs.existsSync(publicDir)) {
    fs.mkdirSync(publicDir, { recursive: true });
  }

  // Copy file
  fs.copyFileSync(sourceFile, targetFile);

  console.log('✓ AudioWorklet processor copied successfully');
  console.log(`  From: ${path.relative(process.cwd(), sourceFile)}`);
  console.log(`  To:   ${path.relative(process.cwd(), targetFile)}`);
} catch (error) {
  console.error('✗ Failed to copy AudioWorklet processor:', error.message);
  process.exit(1);
}
