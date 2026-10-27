import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:livekit_client/livekit_client.dart';

import '../../../core/i18n/app_localizations.dart';
import '../../../core/widgets/app_avatar.dart';
import '../data/call_controller.dart';
import '../domain/call_models.dart';

/// Lớp phủ cuộc gọi toàn cục: hiện cuộc gọi đến/đi/đang gọi trên mọi màn hình.
class CallOverlay extends ConsumerWidget {
  const CallOverlay({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(callControllerProvider);
    return Stack(
      fit: StackFit.expand,
      children: [
        child,
        if (!state.isIdle) Positioned.fill(child: CallScreen(state: state)),
      ],
    );
  }
}

class CallScreen extends ConsumerWidget {
  const CallScreen({super.key, required this.state});

  final CallState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final controller = ref.read(callControllerProvider.notifier);

    return Material(
      color: const Color(0xFF101318),
      child: SafeArea(
        child: switch (state.phase) {
          CallPhase.incoming => _IncomingView(state: state),
          CallPhase.ended => _EndedView(state: state),
          _ => _ActiveView(
              state: state,
              onToggleMic: controller.toggleMicrophone,
              onToggleCamera: controller.toggleCamera,
              onEnd: controller.end,
            ),
        },
      ),
    );
  }
}

class _IncomingView extends ConsumerWidget {
  const _IncomingView({required this.state});

  final CallState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final call = state.call;
    final controller = ref.read(callControllerProvider.notifier);
    final callerNames = call?.participants
            .where((participant) => participant.userId == call.initiatedBy)
            .map((participant) => participant.displayName)
            .join(', ') ??
        '';

    return Column(
      children: [
        const Spacer(),
        AppAvatar(name: callerNames.isEmpty ? '?' : callerNames, radius: 52),
        const SizedBox(height: 20),
        Text(
          call?.isVideo == true ? l10n.t('call_incoming_video') : l10n.t('call_incoming_audio'),
          style: const TextStyle(color: Colors.white70, fontSize: 16),
        ),
        const SizedBox(height: 8),
        Text(
          callerNames,
          textAlign: TextAlign.center,
          style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.w700),
        ),
        const Spacer(),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceEvenly,
          children: [
            _RoundAction(
              icon: Icons.call_end,
              label: l10n.t('call_decline'),
              color: Colors.red,
              onPressed: controller.decline,
            ),
            _RoundAction(
              icon: call?.isVideo == true ? Icons.videocam : Icons.call,
              label: l10n.t('call_answer'),
              color: Colors.green,
              onPressed: controller.accept,
            ),
          ],
        ),
        const SizedBox(height: 40),
      ],
    );
  }
}

class _ActiveView extends ConsumerWidget {
  const _ActiveView({
    required this.state,
    required this.onToggleMic,
    required this.onToggleCamera,
    required this.onEnd,
  });

  final CallState state;
  final Future<void> Function() onToggleMic;
  final Future<void> Function() onToggleCamera;
  final Future<void> Function() onEnd;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final room = ref.read(callControllerProvider.notifier).room;
    final call = state.call;
    final participants = <Participant>[
      if (room?.localParticipant != null) room!.localParticipant!,
      ...?room?.remoteParticipants.values,
    ];
    final isVideo = call?.isVideo == true;

    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 4),
          child: Row(
            children: [
              Icon(
                isVideo ? Icons.videocam : Icons.call,
                color: Colors.white70,
                size: 18,
              ),
              const SizedBox(width: 6),
              Text(
                state.phase == CallPhase.outgoing
                    ? l10n.t('call_outgoing')
                    : state.phase == CallPhase.connecting
                        ? l10n.t('call_connecting')
                        : (isVideo ? l10n.t('call_video') : l10n.t('call_audio')),
                style: const TextStyle(color: Colors.white70, fontSize: 13),
              ),
              const Spacer(),
              if (call?.answeredAt != null)
                _CallTimer(answeredAt: call!.answeredAt!),
              const SizedBox(width: 8),
              if (participants.isEmpty)
                const SizedBox(
                  width: 14,
                  height: 14,
                  child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white54),
                ),
            ],
          ),
        ),
        Expanded(
          child: participants.isEmpty
              ? Center(
                  child: Text(
                    l10n.t('call_waiting_others'),
                    style: const TextStyle(color: Colors.white54),
                  ),
                )
              : _ParticipantGrid(
                  participants: participants,
                  call: call,
                  isVideo: isVideo,
                ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 8, 20, 18),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceEvenly,
            children: [
              _ControlButton(
                icon: state.micEnabled ? Icons.mic : Icons.mic_off,
                active: state.micEnabled,
                onPressed: onToggleMic,
              ),
              if (isVideo)
                _ControlButton(
                  icon: state.cameraEnabled ? Icons.videocam : Icons.videocam_off,
                  active: state.cameraEnabled,
                  onPressed: onToggleCamera,
                ),
              _ControlButton(
                icon: Icons.call_end,
                color: Colors.red,
                onPressed: onEnd,
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _ParticipantGrid extends StatelessWidget {
  const _ParticipantGrid({
    required this.participants,
    required this.call,
    required this.isVideo,
  });

  final List<Participant> participants;
  final CallInfo? call;
  final bool isVideo;

  @override
  Widget build(BuildContext context) {
    if (participants.length == 1) {
      return _ParticipantTile(
        participant: participants.first,
        call: call,
        isVideo: isVideo,
      );
    }

    return GridView.builder(
      padding: const EdgeInsets.all(8),
      itemCount: participants.length,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2,
        mainAxisSpacing: 8,
        crossAxisSpacing: 8,
      ),
      itemBuilder: (context, index) => _ParticipantTile(
        participant: participants[index],
        call: call,
        isVideo: isVideo,
      ),
    );
  }
}

class _ParticipantTile extends StatelessWidget {
  const _ParticipantTile({
    required this.participant,
    required this.call,
    required this.isVideo,
  });

  final Participant participant;
  final CallInfo? call;
  final bool isVideo;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    final isLocal = participant is LocalParticipant;
    var name = isLocal ? l10n.t('call_you') : participant.identity;
    for (final item in call?.participants ?? const <CallParticipant>[]) {
      if (item.userId == participant.identity) {
        name = item.displayName;
        break;
      }
    }

    VideoTrack? track;
    if (isVideo) {
      for (final publication in participant.videoTrackPublications) {
        final candidate = publication.track;
        if (candidate is VideoTrack && !candidate.muted) {
          track = candidate;
          break;
        }
      }
    }

    return ClipRRect(
      borderRadius: BorderRadius.circular(14),
      child: ColoredBox(
        color: const Color(0xFF1C222B),
        child: Stack(
          fit: StackFit.expand,
          children: [
            if (track != null)
              VideoTrackRenderer(
                track,
                fit: VideoViewFit.cover,
                mirrorMode: isLocal ? VideoViewMirrorMode.mirror : VideoViewMirrorMode.off,
              )
            else
              Center(
                child: AppAvatar(name: name, radius: 34),
              ),
            Positioned(
              left: 8,
              bottom: 8,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: Colors.black54,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  name,
                  style: const TextStyle(color: Colors.white, fontSize: 12),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EndedView extends ConsumerWidget {
  const _EndedView({required this.state});

  final CallState state;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final l10n = AppLocalizations.of(context);
    final controller = ref.read(callControllerProvider.notifier);

    return Column(
      children: [
        const Spacer(),
        Icon(
          state.errorCode == null ? Icons.call_end : Icons.error_outline,
          size: 54,
          color: state.errorCode == null ? Colors.white70 : Colors.redAccent,
        ),
        const SizedBox(height: 16),
        Text(
          state.errorCode == null ? l10n.t('call_ended_title') : l10n.t('call_error'),
          style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.w700),
        ),
        if (state.errorCode != null)
          Padding(
            padding: const EdgeInsets.fromLTRB(24, 8, 24, 0),
            child: Text(
              l10n.error(state.errorCode),
              textAlign: TextAlign.center,
              style: const TextStyle(color: Colors.white70),
            ),
          ),
        const Spacer(),
        TextButton(
          onPressed: controller.dismissEnded,
          child: Text(l10n.t('common_close')),
        ),
        const SizedBox(height: 24),
      ],
    );
  }
}

class _CallTimer extends StatefulWidget {
  const _CallTimer({required this.answeredAt});

  final DateTime answeredAt;

  @override
  State<_CallTimer> createState() => _CallTimerState();
}

class _CallTimerState extends State<_CallTimer> {
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) {
        setState(() {});
      }
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final seconds = DateTime.now().difference(widget.answeredAt).inSeconds.clamp(0, 86400);
    final hours = seconds ~/ 3600;
    final minutes = (seconds % 3600) ~/ 60;
    final remaining = seconds % 60;
    final text = hours > 0
        ? '${hours.toString().padLeft(2, '0')}:${minutes.toString().padLeft(2, '0')}:${remaining.toString().padLeft(2, '0')}'
        : '${minutes.toString().padLeft(2, '0')}:${remaining.toString().padLeft(2, '0')}';
    return Text(
      text,
      style: const TextStyle(color: Colors.white70, fontSize: 13),
    );
  }
}

class _ControlButton extends StatelessWidget {  const _ControlButton({
    required this.icon,
    required this.onPressed,
    this.active = true,
    this.color,
  });

  final IconData icon;
  final Future<void> Function() onPressed;
  final bool active;
  final Color? color;

  @override
  Widget build(BuildContext context) {
    return IconButton.filled(
      onPressed: () => onPressed(),
      style: IconButton.styleFrom(
        backgroundColor: color ?? (active ? Colors.white24 : Colors.white10),
        foregroundColor: Colors.white,
        padding: const EdgeInsets.all(16),
      ),
      icon: Icon(icon),
    );
  }
}

class _RoundAction extends StatelessWidget {
  const _RoundAction({
    required this.icon,
    required this.label,
    required this.color,
    required this.onPressed,
  });

  final IconData icon;
  final String label;
  final Color color;
  final Future<void> Function() onPressed;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        IconButton.filled(
          onPressed: () => onPressed(),
          style: IconButton.styleFrom(
            backgroundColor: color,
            foregroundColor: Colors.white,
            padding: const EdgeInsets.all(18),
          ),
          icon: Icon(icon),
        ),
        const SizedBox(height: 6),
        Text(label, style: const TextStyle(color: Colors.white70, fontSize: 12)),
      ],
    );
  }
}
