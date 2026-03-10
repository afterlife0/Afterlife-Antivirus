#!/usr/bin/env python3
"""
Convert LightGBM models to ONNX format for C# integration.
This script converts all LightGBM .txt models in the models_ensemble folder to ONNX.

Requirements:
    pip install lightgbm onnxmltools skl2onnx onnxruntime numpy
"""

import os
import sys
from pathlib import Path

import numpy as np
import lightgbm as lgb

# Try to import onnxmltools
try:
    import onnxmltools
    from onnxmltools.convert import convert_lightgbm
    from onnxmltools.convert.common.data_types import FloatTensorType
except ImportError:
    print("ERROR: onnxmltools not installed. Run: pip install onnxmltools skl2onnx")
    sys.exit(1)

def get_feature_count_from_model(model_path):
    """Load a LightGBM model and get the number of features"""
    model = lgb.Booster(model_file=str(model_path))
    feature_names = model.feature_name()
    return len(feature_names), feature_names

def convert_lgb_to_onnx(model_path, output_path, num_features):
    """Convert a LightGBM model to ONNX format"""
    print(f"Converting: {model_path}")
    
    # Load the LightGBM model
    model = lgb.Booster(model_file=str(model_path))
    
    # Define input type (batch_size=None means dynamic batch size)
    # Features are float32
    initial_type = [('features', FloatTensorType([None, num_features]))]
    
    # Convert to ONNX
    onnx_model = convert_lightgbm(
        model, 
        initial_types=initial_type,
        target_opset=12  # Use opset 12 for broad compatibility
    )
    
    # Save ONNX model
    onnxmltools.utils.save_model(onnx_model, str(output_path))
    print(f"  -> Saved: {output_path}")
    
    return True

def main():
    # Paths
    script_dir = Path(__file__).parent
    models_dir = script_dir / "models_ensemble"
    output_dir = script_dir / "resources" / "ai_models"
    
    # Create output directory
    output_dir.mkdir(parents=True, exist_ok=True)
    
    print("=" * 60)
    print("LightGBM to ONNX Converter")
    print("=" * 60)
    
    # Find all LightGBM model files
    model_files = list(models_dir.glob("*.txt"))
    
    if not model_files:
        print(f"No model files found in: {models_dir}")
        return
    
    print(f"Found {len(model_files)} models to convert")
    print()
    
    # Store feature info for C# code generation
    feature_info = {}
    
    for model_path in model_files:
        try:
            # Get feature count
            num_features, feature_names = get_feature_count_from_model(model_path)
            print(f"Model: {model_path.name}")
            print(f"  Features: {num_features}")
            
            # Output path
            output_path = output_dir / f"{model_path.stem}.onnx"
            
            # Convert
            success = convert_lgb_to_onnx(model_path, output_path, num_features)
            
            if success:
                feature_info[model_path.stem] = {
                    'num_features': num_features,
                    'feature_names': feature_names
                }
            
            print()
            
        except Exception as e:
            print(f"  ERROR: {e}")
            print()
    
    # Save feature names to JSON for C# reference
    import json
    feature_info_path = output_dir / "model_features.json"
    with open(feature_info_path, 'w') as f:
        json.dump(feature_info, f, indent=2)
    print(f"Feature info saved to: {feature_info_path}")
    
    # Print summary
    print()
    print("=" * 60)
    print("Conversion Complete!")
    print(f"ONNX models saved to: {output_dir}")
    print("=" * 60)

if __name__ == "__main__":
    main()
