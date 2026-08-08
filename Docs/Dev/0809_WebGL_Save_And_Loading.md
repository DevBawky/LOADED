# WebGL 저장과 로딩 최적화

## 저장 및 불러오기

`RunSaveSystem`은 데스크톱에서는 기존처럼 `Application.persistentDataPath`의 JSON 파일을 사용한다. WebGL에서는 같은 JSON을 `loaded.run.save.v3` PlayerPrefs 키에 저장하고 즉시 `PlayerPrefs.Save()`를 호출한다. 이전 WebGL 빌드에서 가상 파일 시스템 저장이 남아 있으면 최초 불러오기 때 새 저장소로 가져온다.

WebGL은 탭 종료와 새로고침 때 종료 콜백을 보장하지 않는다. 따라서 첫 전투 시작, 적 턴 처리 완료, 상점 상품 변경과 구매 완료처럼 상태가 안정된 시점마다 자동 체크포인트를 저장한다.

브라우저 저장은 같은 도메인과 경로에서 유지된다. 시크릿 모드, 사이트 데이터 삭제, 브라우저 정책에 따른 IndexedDB 제거 후에는 복구할 수 없다.

전용 WebGL 템플릿은 `autoSyncPersistentDataPath`도 활성화해 다른 파일 기반 저장이 추가되더라도 IndexedDB 동기화가 이루어지도록 한다.

## 로딩 최적화

- Brotli 압축과 Unity 데이터 캐싱 유지
- 빌드 파일명을 해시로 생성하고 변경되지 않은 데이터, 프레임워크, WASM을 재방문 시 재사용
- HTML에서 데이터와 WASM 다운로드를 로더보다 먼저 시작
- 모바일 렌더링 배율을 1로 제한해 시작 메모리와 GPU 부담 감소
- WebGL 초기 메모리를 128MB로 설정해 시작 직후 반복적인 힙 확장 감소
- Managed Stripping Level을 Medium으로 설정
- BGM만 WebGL 전용 Vorbis 품질 0.55와 최적 샘플 레이트로 빌드
- 가이드와 배경 영상은 StreamingAssets에 두어 최초 Unity 데이터 다운로드에 합치지 않고 사용 시 요청

`Tools > LOADED > Build WebGL`은 최적화 설정을 적용한 뒤 빌드한다. 설정만 다시 적용하려면 `Tools > LOADED > Apply WebGL Optimizations`를 사용한다.

서버는 `.unityweb` 파일을 그대로 제공할 수 있어야 한다. 현재는 배포처 호환성을 위해 Decompression Fallback을 유지한다. 서버가 Brotli `Content-Encoding`을 확실히 지원한다면 Fallback을 끄면 브라우저 측 압축 해제 비용을 더 줄일 수 있다.
