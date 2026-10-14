# Local model files

Place one compatible GGUF language model in this folder. The apps detect the model automatically; there is no normal-operation model picker.

Discovery priority: helalm-4b.Q8_0.gguf when present and complete; otherwise Qwen2.5-3B; otherwise Qwen2.5-1.5B; otherwise any other complete GGUF file in name order.

The application rejects files smaller than 10,000,000 bytes, but this does not prove model compatibility or license. Check the model card, license, hash where available, and required RAM. No model is downloaded during normal app startup.

The existing legacy setup script at ../scripts/Prepare-OfflineBundle.ps1 can prepare Qwen2.5 1.5B or 3B Q4_K_M files in an explicit setup workflow. The new suite workflow's optional model input places that prepared model beside NENAPIYUMA Chat so Studio can find the same copy.
