use std::{env, fs, path::PathBuf};

#[tauri::command]
fn get_api_key() -> Result<String, String> {
  let program_data = env::var_os("PROGRAMDATA")
    .map(PathBuf::from)
    .unwrap_or_else(|| PathBuf::from(r"C:\ProgramData"));
  let config_path = program_data.join("LightShield").join("lightshield_config.json");
  let contents = fs::read_to_string(&config_path)
    .map_err(|e| format!("Unable to read LightShield API configuration: {e}"))?;
  let config: serde_json::Value = serde_json::from_str(&contents)
    .map_err(|e| format!("Invalid LightShield API configuration: {e}"))?;
  config.get("apiKey")
    .and_then(|value| value.as_str())
    .filter(|value| value.len() >= 32)
    .map(str::to_owned)
    .ok_or_else(|| "LightShield API credential is missing.".to_string())
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  tauri::Builder::default()
    .invoke_handler(tauri::generate_handler![get_api_key])
    .setup(|app| {
      if cfg!(debug_assertions) {
        app.handle().plugin(
          tauri_plugin_log::Builder::default()
            .level(log::LevelFilter::Info)
            .build(),
        )?;
      }
      Ok(())
    })
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
