#!/usr/bin/env bash
# Concatenate head.html + src/*.js into index.html (single file, publishable as an Artifact).
set -euo pipefail
cd "$(dirname "$0")"
{
  cat head.html
  echo '<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>'
  for f in postprocessing/EffectComposer postprocessing/RenderPass postprocessing/ShaderPass shaders/CopyShader shaders/LuminosityHighPassShader postprocessing/UnrealBloomPass shaders/FXAAShader objects/Sky; do
    echo "<script src=\"https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/$f.js\"></script>"
  done
  echo '<script>'
  cat src/*.js
  echo '</script>'
} > index.html
echo "built index.html ($(wc -c < index.html) bytes)"
