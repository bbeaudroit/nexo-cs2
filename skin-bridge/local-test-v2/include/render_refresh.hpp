#pragma once
namespace localtest {
// Same bounded identity/pending guard before each native call; no callback address input.
template<class Backend> bool regenerateAndRefresh(Backend& backend){
 if(!backend.ready()||!backend.nativeRegen())return false;
 if(!backend.ready()||!backend.mark())return false;
 if(!backend.ready()||!backend.consume())return false;
 return true;
}
}
